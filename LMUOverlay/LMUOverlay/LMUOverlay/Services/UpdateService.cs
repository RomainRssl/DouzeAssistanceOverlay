using AutoUpdaterDotNET;

namespace LMUOverlay.Services
{
    /// <summary>
    /// Vérifie les mises à jour depuis GitHub Releases via AutoUpdater.NET.
    /// update.xml doit être hébergé à l'URL ci-dessous (fichier commité dans le repo).
    /// </summary>
    public static class UpdateService
    {
        private const string UpdateXmlUrl =
            "https://raw.githubusercontent.com/RomainRssl/DouzeAssistanceOverlay/main/update.xml";

        /// <summary>
        /// Vérifie les mises à jour.
        /// En mode silencieux (silent=true), ne montre rien si aucune mise à jour n'est disponible.
        /// </summary>
        public static void CheckForUpdates(bool silent = true)
        {
            AutoUpdater.RunUpdateAsAdmin = true;
            AutoUpdater.ShowSkipButton = true;
            AutoUpdater.ReportErrors = !silent;
            AutoUpdater.Start(UpdateXmlUrl);
        }
    }
}
