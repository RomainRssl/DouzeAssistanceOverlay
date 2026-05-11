/*
rF2 internal state mapping structures.  Allows access to native C++ structs from C#.
Must be kept in sync with Include\rF2State.h.

See: MainForm.MainUpdate for sample on how to marshall from native in memory struct.

Author: The Iron Wolf (vleonavicius@hotmail.com)
Website: thecrewchief.org
*/
using System;
using System.Runtime.InteropServices;

namespace rF2SharedMemory
{
  // Marshalled types:
  // C++                 C#
  // char          ->    byte
  // unsigned char ->    byte
  // signed char   ->    sbyte
  // bool          ->    byte
  // long          ->    int
  // unsigned long ->    uint
  // short         ->    short
  // unsigned short ->   ushort
  // ULONGLONG     ->    Int64
  public class rFactor2Constants
  {
    public const string MM_TELEMETRY_FILE_NAME = "$rFactor2SMMP_Telemetry$";
    public const string MM_SCORING_FILE_NAME = "$rFactor2SMMP_Scoring$";
    public const string MM_RULES_FILE_NAME = "$rFactor2SMMP_Rules$";
    public const string MM_FORCE_FEEDBACK_FILE_NAME = "$rFactor2SMMP_ForceFeedback$";
    public const string MM_GRAPHICS_FILE_NAME = "$rFactor2SMMP_Graphics$";
    public const string MM_PITINFO_FILE_NAME = "$rFactor2SMMP_PitInfo$";
    public const string MM_WEATHER_FILE_NAME = "$rFactor2SMMP_Weather$";
    public const string MM_EXTENDED_FILE_NAME = "$rFactor2SMMP_Extended$";

    public const string MM_HWCONTROL_FILE_NAME = "$rFactor2SMMP_HWControl$";
    public const int MM_HWCONTROL_LAYOUT_VERSION = 1;

    public const string MM_WEATHER_CONTROL_FILE_NAME = "$rFactor2SMMP_WeatherControl$";
    public const int MM_WEATHER_CONTROL_LAYOUT_VERSION = 1;

    public const string MM_RULES_CONTROL_FILE_NAME = "$rFactor2SMMP_RulesControl$";
    public const int MM_RULES_CONTROL_LAYOUT_VERSION = 1;

    public const string MM_PLUGIN_CONTROL_FILE_NAME = "$rFactor2SMMP_PluginControl$";
    public const int MM_PLUGIN_CONTROL_LAYOUT_VERSION = 1;

    public const int MAX_MAPPED_VEHICLES = 128;
    public const int MAX_MAPPED_IDS = 512;
    public const int MAX_STATUS_MSG_LEN = 128;
    public const int MAX_RULES_INSTRUCTION_MSG_LEN = 96;
    public const int MAX_HWCONTROL_NAME_LEN = 96;
    public const string RFACTOR2_PROCESS_NAME = "rFactor2";

    public const byte RowX = 0;
    public const byte RowY = 1;
    public const byte RowZ = 2;

    public enum rF2GamePhase
    {
      Garage = 0, WarmUp = 1, GridWalk = 2, Formation = 3,
      Countdown = 4, GreenFlag = 5, FullCourseYellow = 6,
      SessionStopped = 7, SessionOver = 8, PausedOrHeartbeat = 9
    }

    public enum rF2YellowFlagState
    {
      Invalid = -1, NoFlag = 0, Pending = 1, PitClosed = 2,
      PitLeadLap = 3, PitOpen = 4, LastLap = 5, Resume = 6, RaceHalt = 7
    }

    public enum rF2SurfaceType
    {
      Dry = 0, Wet = 1, Grass = 2, Dirt = 3,
      Gravel = 4, Kerb = 5, Special = 6
    }

    public enum rF2Sector { Sector3 = 0, Sector1 = 1, Sector2 = 2 }
    public enum rF2FinishStatus { None = 0, Finished = 1, Dnf = 2, Dq = 3 }
    public enum rF2Control { Nobody = -1, Player = 0, AI = 1, Remote = 2, Replay = 3 }
    public enum rF2WheelIndex { FrontLeft = 0, FrontRight = 1, RearLeft = 2, RearRight = 3 }
    public enum rF2PitState { None = 0, Request = 1, Entering = 2, Stopped = 3, Exiting = 4 }
    public enum rF2PrimaryFlag { Green = 0, Blue = 6 }
    public enum rF2CountLapFlag { DoNotCountLap = 0, CountLapButNotTime = 1, CountLapAndTime = 2 }
    public enum rF2RearFlapLegalStatus { Disallowed = 0, DetectedButNotAllowedYet = 1, Alllowed = 2 }
    public enum rF2IgnitionStarterStatus { Off = 0, Ignition = 1, IgnitionAndStarter = 2 }
    public enum rF2SafetyCarInstruction { NoChange = 0, GoActive = 1, HeadForPits = 2 }
  }

  namespace rFactor2Data
  {
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct rF2Vec3 { public double x, y, z; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2Wheel
    {
      public double mSuspensionDeflection;
      public double mRideHeight;
      public double mSuspForce;
      public double mBrakeTemp;
      public double mBrakePressure;
      public double mRotation;
      public double mLateralPatchVel;
      public double mLongitudinalPatchVel;
      public double mLateralGroundVel;
      public double mLongitudinalGroundVel;
      public double mCamber;
      public double mLateralForce;
      public double mLongitudinalForce;
      public double mTireLoad;
      public double mGripFract;
      public double mPressure;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
      public double[] mTemperature;
      public double mWear;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
      public byte[] mTerrainName;
      public byte mSurfaceType;
      public byte mFlat;
      public byte mDetached;
      public byte mStaticUndeflectedRadius;
      public double mVerticalTireDeflection;
      public double mWheelYLocation;
      public double mToe;
      public double mTireCarcassTemperature;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
      public double[] mTireInnerLayerTemperature;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 24)]
      byte[] mExpansion;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2VehicleTelemetry
    {
      public int mID;
      public double mDeltaTime;
      public double mElapsedTime;
      public int mLapNumber;
      public double mLapStartET;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
      public byte[] mVehicleName;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
      public byte[] mTrackName;
      public rF2Vec3 mPos;
      public rF2Vec3 mLocalVel;
      public rF2Vec3 mLocalAccel;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
      public rF2Vec3[] mOri;
      public rF2Vec3 mLocalRot;
      public rF2Vec3 mLocalRotAccel;
      public int mGear;
      public double mEngineRPM;
      public double mEngineWaterTemp;
      public double mEngineOilTemp;
      public double mClutchRPM;
      public double mUnfilteredThrottle;
      public double mUnfilteredBrake;
      public double mUnfilteredSteering;
      public double mUnfilteredClutch;
      public double mFilteredThrottle;
      public double mFilteredBrake;
      public double mFilteredSteering;
      public double mFilteredClutch;
      public double mSteeringShaftTorque;
      public double mFront3rdDeflection;
      public double mRear3rdDeflection;
      public double mFrontWingHeight;
      public double mFrontRideHeight;
      public double mRearRideHeight;
      public double mDrag;
      public double mFrontDownforce;
      public double mRearDownforce;
      public double mFuel;
      public double mEngineMaxRPM;
      public byte mScheduledStops;
      public byte mOverheating;
      public byte mDetached;
      public byte mHeadlights;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
      public byte[] mDentSeverity;
      public double mLastImpactET;
      public double mLastImpactMagnitude;
      public rF2Vec3 mLastImpactPos;
      public double mEngineTorque;
      public int mCurrentSector;
      public byte mSpeedLimiter;
      public byte mMaxGears;
      public byte mFrontTireCompoundIndex;
      public byte mRearTireCompoundIndex;
      public double mFuelCapacity;
      public byte mFrontFlapActivated;
      public byte mRearFlapActivated;
      public byte mRearFlapLegalStatus;
      public byte mIgnitionStarter;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 18)]
      public byte[] mFrontTireCompoundName;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 18)]
      public byte[] mRearTireCompoundName;
      public byte mSpeedLimiterAvailable;
      public byte mAntiStallActivated;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
      public byte[] mUnused;
      public float mVisualSteeringWheelRange;
      public double mRearBrakeBias;
      public double mTurboBoostPressure;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
      public float[] mPhysicsToGraphicsOffset;
      public float mPhysicalSteeringWheelRange;
      public double mBatteryChargeFraction;
      public double mElectricBoostMotorTorque;
      public double mElectricBoostMotorRPM;
      public double mElectricBoostMotorTemperature;
      public double mElectricBoostWaterTemperature;
      public byte mElectricBoostMotorState;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 111)]
      public byte[] mExpansion;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4)]
      public rF2Wheel[] mWheels;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2ScoringInfo
    {
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
      public byte[] mTrackName;
      public int mSession;
      public double mCurrentET;
      public double mEndET;
      public int mMaxLaps;
      public double mLapDist;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
      public byte[] pointer1;
      public int mNumVehicles;
      public byte mGamePhase;
      public sbyte mYellowFlagState;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
      public sbyte[] mSectorFlag;
      public byte mStartLight;
      public byte mNumRedLights;
      public byte mInRealtime;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
      public byte[] mPlayerName;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
      public byte[] mPlrFileName;
      public double mDarkCloud;
      public double mRaining;
      public double mAmbientTemp;
      public double mTrackTemp;
      public rF2Vec3 mWind;
      public double mMinPathWetness;
      public double mMaxPathWetness;
      public byte mGameMode;
      public byte mIsPasswordProtected;
      public ushort mServerPort;
      public uint mServerPublicIP;
      public int mMaxPlayers;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
      public byte[] mServerName;
      public float mStartET;
      public double mAvgPathWetness;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 200)]
      public byte[] mExpansion;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
      public byte[] pointer2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2VehicleScoring
    {
      public int mID;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
      public byte[] mDriverName;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
      public byte[] mVehicleName;
      public short mTotalLaps;
      public sbyte mSector;
      public sbyte mFinishStatus;
      public double mLapDist;
      public double mPathLateral;
      public double mTrackEdge;
      public double mBestSector1;
      public double mBestSector2;
      public double mBestLapTime;
      public double mLastSector1;
      public double mLastSector2;
      public double mLastLapTime;
      public double mCurSector1;
      public double mCurSector2;
      public short mNumPitstops;
      public short mNumPenalties;
      public byte mIsPlayer;
      public sbyte mControl;
      public byte mInPits;
      public byte mPlace;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
      public byte[] mVehicleClass;
      public double mTimeBehindNext;
      public int mLapsBehindNext;
      public double mTimeBehindLeader;
      public int mLapsBehindLeader;
      public double mLapStartET;
      public rF2Vec3 mPos;
      public rF2Vec3 mLocalVel;
      public rF2Vec3 mLocalAccel;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
      public rF2Vec3[] mOri;
      public rF2Vec3 mLocalRot;
      public rF2Vec3 mLocalRotAccel;
      public byte mHeadlights;
      public byte mPitState;
      public byte mServerScored;
      public byte mIndividualPhase;
      public int mQualification;
      public double mTimeIntoLap;
      public double mEstimatedLapTime;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 24)]
      public byte[] mPitGroup;
      public byte mFlag;
      public byte mUnderYellow;
      public byte mCountLapFlag;
      public byte mInGarageStall;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
      public byte[] mUpgradePack;
      public float mPitLapDist;
      public float mBestLapSector1;
      public float mBestLapSector2;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 48)]
      public byte[] mExpansion;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct rF2PhysicsOptions
    {
      public byte mTractionControl;
      public byte mAntiLockBrakes;
      public byte mStabilityControl;
      public byte mAutoShift;
      public byte mAutoClutch;
      public byte mInvulnerable;
      public byte mOppositeLock;
      public byte mSteeringHelp;
      public byte mBrakingHelp;
      public byte mSpinRecovery;
      public byte mAutoPit;
      public byte mAutoLift;
      public byte mAutoBlip;
      public byte mFuelMult;
      public byte mTireMult;
      public byte mMechFail;
      public byte mAllowPitcrewPush;
      public byte mRepeatShifts;
      public byte mHoldClutch;
      public byte mAutoReverse;
      public byte mAlternateNeutral;
      public byte mAIControl;
      public byte mUnused1;
      public byte mUnused2;
      public float mManualShiftOverrideTime;
      public float mAutoShiftOverrideTime;
      public float mSpeedSensitiveSteering;
      public float mSteerRatioSpeed;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct rF2TrackedDamage
    {
      public double mMaxImpactMagnitude;
      public double mAccumulatedImpactMagnitude;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct rF2VehScoringCapture
    {
      public int mID;
      public byte mPlace;
      public byte mIsPlayer;
      public sbyte mFinishStatus;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct rF2SessionTransitionCapture
    {
      public byte mGamePhase;
      public int mSession;
      public int mNumScoringVehicles;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = rFactor2Constants.MAX_MAPPED_VEHICLES)]
      public rF2VehScoringCapture[] mScoringVehicles;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2MappedBufferVersionBlock
    {
      public uint mVersionUpdateBegin;
      public uint mVersionUpdateEnd;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2Telemetry
    {
      public uint mVersionUpdateBegin;
      public uint mVersionUpdateEnd;
      public int mBytesUpdatedHint;
      public int mNumVehicles;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = rFactor2Constants.MAX_MAPPED_VEHICLES)]
      public rF2VehicleTelemetry[] mVehicles;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2Scoring
    {
      public uint mVersionUpdateBegin;
      public uint mVersionUpdateEnd;
      public int mBytesUpdatedHint;
      public rF2ScoringInfo mScoringInfo;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = rFactor2Constants.MAX_MAPPED_VEHICLES)]
      public rF2VehicleScoring[] mVehicles;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2Extended
    {
      public uint mVersionUpdateBegin;
      public uint mVersionUpdateEnd;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 12)]
      public byte[] mVersion;
      public byte is64bit;
      public rF2PhysicsOptions mPhysics;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = rFactor2Constants.MAX_MAPPED_IDS)]
      public rF2TrackedDamage[] mTrackedDamages;
      public byte mInRealtimeFC;
      public byte mMultimediaThreadStarted;
      public byte mSimulationThreadStarted;
      public byte mSessionStarted;
      public Int64 mTicksSessionStarted;
      public Int64 mTicksSessionEnded;
      public rF2SessionTransitionCapture mSessionTransitionCapture;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 128)]
      public byte[] mDisplayedMessageUpdateCapture;
      public byte mDirectMemoryAccessEnabled;
      public Int64 mTicksStatusMessageUpdated;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = rFactor2Constants.MAX_STATUS_MSG_LEN)]
      public byte[] mStatusMessage;
      public Int64 mTicksLastHistoryMessageUpdated;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = rFactor2Constants.MAX_STATUS_MSG_LEN)]
      public byte[] mLastHistoryMessage;
      public float mCurrentPitSpeedLimit;
      public byte mSCRPluginEnabled;
      public int mSCRPluginDoubleFileType;
      public Int64 mTicksLSIPhaseMessageUpdated;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = rFactor2Constants.MAX_RULES_INSTRUCTION_MSG_LEN)]
      public byte[] mLSIPhaseMessage;
      public Int64 mTicksLSIPitStateMessageUpdated;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = rFactor2Constants.MAX_RULES_INSTRUCTION_MSG_LEN)]
      public byte[] mLSIPitStateMessage;
      public Int64 mTicksLSIOrderInstructionMessageUpdated;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = rFactor2Constants.MAX_RULES_INSTRUCTION_MSG_LEN)]
      public byte[] mLSIOrderInstructionMessage;
      public Int64 mTicksLSIRulesInstructionMessageUpdated;
      [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = rFactor2Constants.MAX_RULES_INSTRUCTION_MSG_LEN)]
      public byte[] mLSIRulesInstructionMessage;
      public int mUnsubscribedBuffersMask;
      public byte mHWControlInputEnabled;
      public byte mWeatherControlInputEnabled;
      public byte mRulesControlInputEnabled;
      public byte mPluginControlInputEnabled;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2ForceFeedback
    {
      public uint mVersionUpdateBegin;
      public uint mVersionUpdateEnd;
      public double mForceValue;
    }
  }
}
