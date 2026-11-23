using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace Twtl.Mod
{
    [FileLocation("ModsSettings/TwoWayTurnLane/TwoWayTurnLane")]
    public sealed class Settings : ModSetting
    {
        public Settings(IMod mod) : base(mod) { }
        [SettingsUISection("Research", "Tools")]
        public bool ShowClosedResearchLane { get; set; }
        [SettingsUISection("Research", "Tools")]
        public bool ShowNativeFixture { get; set; }
        [SettingsUISection("Research", "Tools")]
        public bool ShowMedianGraphFixture { get; set; }
        [SettingsUISection("Research", "Tools")]
        public bool ShowSeparatorGraphFixture { get; set; }
        [SettingsUISection("Research", "Tools")]
        public bool CaptureSelectedRoad { get; set; }
        public override void SetDefaults() { ShowClosedResearchLane = false; ShowNativeFixture = false; ShowMedianGraphFixture = false; ShowSeparatorGraphFixture = false; CaptureSelectedRoad = false; }
    }

    internal sealed class EnglishLocale : IDictionarySource
    {
        private readonly Settings settings;
        public EnglishLocale(Settings settings) { this.settings = settings; }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                [settings.GetSettingsLocaleID()] = "Two-Way Turn Lane — Research",
                [settings.GetOptionTabLocaleID("Research")] = "Research",
                [settings.GetOptionGroupLocaleID("Tools")] = "Research tools",
                [settings.GetOptionLabelLocaleID(nameof(Settings.ShowClosedResearchLane))] = "Expose closed research corridor",
                [settings.GetOptionDescLocaleID(nameof(Settings.ShowClosedResearchLane))] = "Adds a non-drivable empty section to Road Builder. It is not a working turn lane. Once added, restart to remove it from the picker. Use a disposable test city.",
                [settings.GetOptionLabelLocaleID(nameof(Settings.ShowNativeFixture))] = "Expose unsafe native lane test section",
                [settings.GetOptionDescLocaleID(nameof(Settings.ShowNativeFixture))] = "For a disconnected road in a disposable city only. Adds the game's native 3 m two-way car lane for topology inspection. No entry control, braking proof or turn-only routing. Restart to remove it from the picker.",
                [settings.GetOptionLabelLocaleID(nameof(Settings.ShowMedianGraphFixture))] = "Expose median-flag graph test",
                [settings.GetOptionDescLocaleID(nameof(Settings.ShowMedianGraphFixture))] = "Adds a second disconnected test road with the native two-way lane marked as a median in network metadata. For graph inspection only; no traffic safeguards. Restart to remove it.",
                [settings.GetOptionLabelLocaleID(nameof(Settings.ShowSeparatorGraphFixture))] = "Expose separator graph test",
                [settings.GetOptionDescLocaleID(nameof(Settings.ShowSeparatorGraphFixture))] = "Adds a disconnected road with a zero-width native traffic divider after the center lane. Tests path grouping only; the divider may block lane entry. No car traffic. Restart to remove it.",
                [settings.GetOptionLabelLocaleID(nameof(Settings.CaptureSelectedRoad))] = "Capture selected road once",
                [settings.GetOptionDescLocaleID(nameof(Settings.CaptureSelectedRoad))] = "Select a road first, then enable this to log its lane geometry, overlaps and occupants. Automatically resets; no traffic is modified."
            };
        }
        public void Unload() { }
    }
}
