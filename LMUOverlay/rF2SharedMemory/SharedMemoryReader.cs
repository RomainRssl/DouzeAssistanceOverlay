using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using rF2SharedMemory.rFactor2Data;

namespace rF2SharedMemory
{
    /// <summary>
    /// Reads a shared memory buffer using ViewStream + GCHandle + Marshal.PtrToStructure.
    /// Compatible with MarshalAs attributed managed arrays from the official rF2Data structs.
    /// </summary>
    public class MappedBuffer<T> : IDisposable where T : struct
    {
        private readonly string _mapName;
        private MemoryMappedFile? _mmf;
        private MemoryMappedViewStream? _stream;
        private readonly int _structSize;
        private byte[]? _buffer;
        private readonly byte[] _versionBytes = new byte[8];
        private uint _lastVersion = uint.MaxValue; // force first full read
        private T _cached = default;

        public bool IsConnected { get; private set; }

        public MappedBuffer(string mapName)
        {
            _mapName = mapName;
            _structSize = Marshal.SizeOf(typeof(T));
        }

        public bool Connect()
        {
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(_mapName);
                _stream = _mmf.CreateViewStream(0, _structSize, MemoryMappedFileAccess.Read);
                _buffer = new byte[_structSize];
                IsConnected = true;
                return true;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
        {
            IsConnected = false;
            _stream?.Dispose(); _stream = null;
            _mmf?.Dispose(); _mmf = null;
            _buffer = null;
            _lastVersion = uint.MaxValue;
            _cached = default;
        }

        public T Read()
        {
            if (!IsConnected || _stream == null || _buffer == null)
                return default;

            try
            {
                // Fast path: peek only 8 version bytes to check if data changed
                _stream.Position = 0;
                if (_stream.Read(_versionBytes, 0, 8) < 8) return _cached;
                uint vBegin = BitConverter.ToUInt32(_versionBytes, 0);
                uint vEnd   = BitConverter.ToUInt32(_versionBytes, 4);

                // If version unchanged and stable (even = no write in progress), return cache
                if (vBegin == _lastVersion && vBegin == vEnd && (vBegin & 1) == 0)
                    return _cached;

                // Data changed — full read with spinlock
                const int maxRetries = 3;
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    _stream.Position = 0;
                    int bytesRead = _stream.Read(_buffer, 0, _structSize);
                    if (bytesRead < _structSize) return _cached;

                    vBegin = BitConverter.ToUInt32(_buffer, 0);
                    vEnd   = BitConverter.ToUInt32(_buffer, 4);
                    if (vBegin != vEnd) continue; // game was writing, retry

                    GCHandle handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
                    try
                    {
                        _cached = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!;
                        _lastVersion = vBegin;
                        return _cached;
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                return _cached; // still mid-write after retries → return last known good
            }
            catch
            {
                return default;
            }
        }

        public void Dispose() => Disconnect();
    }

    /// <summary>
    /// Main reader managing all rF2 shared memory buffers.
    /// </summary>
    public class SharedMemoryReader : IDisposable
    {
        private readonly MappedBuffer<rF2Telemetry> _telemetryBuffer;
        private readonly MappedBuffer<rF2Scoring> _scoringBuffer;
        private readonly MappedBuffer<rF2Extended> _extendedBuffer;

        private rF2Telemetry _telemetry;
        private rF2Scoring _scoring;
        private rF2Extended _extended;

        public bool IsConnected { get; private set; }

        public ref readonly rF2Telemetry Telemetry => ref _telemetry;
        public ref readonly rF2Scoring Scoring => ref _scoring;
        public ref readonly rF2Extended Extended => ref _extended;
        public rF2ScoringInfo ScoringInfo => _scoring.mScoringInfo;

        public event EventHandler? Connected;
        public event EventHandler? Disconnected;

        public SharedMemoryReader()
        {
            _telemetryBuffer = new MappedBuffer<rF2Telemetry>(rFactor2Constants.MM_TELEMETRY_FILE_NAME);
            _scoringBuffer = new MappedBuffer<rF2Scoring>(rFactor2Constants.MM_SCORING_FILE_NAME);
            _extendedBuffer = new MappedBuffer<rF2Extended>(rFactor2Constants.MM_EXTENDED_FILE_NAME);
        }

        public bool Connect()
        {
            if (!_telemetryBuffer.Connect()) { IsConnected = false; return false; }
            if (!_scoringBuffer.Connect() || !_extendedBuffer.Connect())
            {
                _telemetryBuffer.Disconnect();
                _scoringBuffer.Disconnect();
                _extendedBuffer.Disconnect();
                IsConnected = false;
                return false;
            }
            IsConnected = true;
            Connected?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void Disconnect()
        {
            bool was = IsConnected;
            IsConnected = false;
            _telemetryBuffer.Disconnect();
            _scoringBuffer.Disconnect();
            _extendedBuffer.Disconnect();
            if (was) Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public void Update()
        {
            if (!IsConnected) return;
            try
            {
                _telemetry = _telemetryBuffer.Read();
                _scoring = _scoringBuffer.Read();
                _extended = _extendedBuffer.Read();
            }
            catch { Disconnect(); }
        }

        public rF2VehicleTelemetry? GetPlayerTelemetry()
        {
            if (!IsConnected || _telemetry.mVehicles == null || _telemetry.mNumVehicles <= 0) return null;

            // Find player by matching with scoring data
            var ps = GetPlayerScoring();
            if (ps == null) return _telemetry.mVehicles[0]; // fallback

            int playerId = ps.Value.mID;
            int n = Math.Min(_telemetry.mNumVehicles, _telemetry.mVehicles.Length);
            for (int i = 0; i < n; i++)
            {
                if (_telemetry.mVehicles[i].mID == playerId)
                    return _telemetry.mVehicles[i];
            }
            return _telemetry.mVehicles[0]; // fallback
        }

        public rF2VehicleScoring? GetPlayerScoring()
        {
            if (!IsConnected || _scoring.mVehicles == null) return null;
            int n = Math.Min(_scoring.mScoringInfo.mNumVehicles, _scoring.mVehicles.Length);
            for (int i = 0; i < n; i++)
                if (_scoring.mVehicles[i].mIsPlayer != 0) return _scoring.mVehicles[i];
            return null;
        }

        public void Dispose()
        {
            Disconnect();
            _telemetryBuffer.Dispose();
            _scoringBuffer.Dispose();
            _extendedBuffer.Dispose();
        }
    }

    /// <summary>
    /// String helper for byte arrays.
    /// </summary>
    public static class rF2Helper
    {
        public static string Str(byte[]? bytes)
        {
            if (bytes == null) return "";
            int len = Array.IndexOf(bytes, (byte)0);
            if (len < 0) len = bytes.Length;
            return len == 0 ? "" : Encoding.UTF8.GetString(bytes, 0, len).Trim();
        }
    }
}
