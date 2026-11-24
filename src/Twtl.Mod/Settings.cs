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
        public bool ShowVirtualLaneGraphFixture { get; set; }
        [SettingsUISection("Research", "Tools")]
        public bool EnableDrivablePrototype { get; set; }
        [SettingsUISection("Research", "Tools")]
        public bool CaptureSelectedRoad { get; set; }
        public override void SetDefaults() { ShowClosedResearchLane = false; ShowNativeFixture = false; ShowMedianGraphFixture = false; ShowSeparatorGraphFixture = false; ShowVirtualLaneGraphFixture = false; EnableDrivablePrototype = false; CaptureSelectedRoad = false; }
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
                [settings.GetOptionDescLocaleID(nameof(Settings.ShowSeparatorGraphFixture))] = "Adds a disconnected road with a zero-width traffic divider after the center lane. Tests path grouping only; the divider may block lane entry. No car traffic. Restart to remove it.",
                [settings.GetOptionLabelLocaleID(nameof(Settings.ShowVirtualLaneGraphFixture))] = "Expose virtual-lane graph test",
                [settings.GetOptionDescLocaleID(nameof(Settings.ShowVirtualLaneGraphFixture))] = "Adds a disconnected road with a zero-width non-road lane after the center. Tests path grouping without a traffic divider. Lateral access and safety are unknown. No car traffic. Restart to remove it.",
                [settings.GetOptionLabelLocaleID(nameof(Settings.EnableDrivablePrototype))] = "Enable drivable prototype in test city",
                [settings.GetOptionDescLocaleID(nameof(Settings.EnableDrivablePrototype))] = "Adds a five-lane Road Builder road with four travel lanes and a shared center open to cars. Turn-only routing and opposing-car safety are NOT implemented. Use only in a disposable test city; do not save it into a valued city. Turning this off closes its center lanes again.",
                [settings.GetOptionLabelLocaleID(nameof(Settings.CaptureSelectedRoad))] = "Capture road and safety probe once",
                [settings.GetOptionDescLocaleID(nameof(Settings.CaptureSelectedRoad))] = "Runs one-time read-only center-lane graph, route and vehicle-turn captures. Select a road first for extra lane geometry and vehicle details. Automatically resets; no traffic is modified."
            };
        }
        public void Unload() { }
    }
}
