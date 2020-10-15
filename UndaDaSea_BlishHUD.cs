using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using CSCore;
using CSCore.SoundOut;
using CSCore.Codecs.MP3;

namespace Taimi.UndaDaSea_BlishHUD
{

    [Export(typeof(Blish_HUD.Modules.Module))]
    public class Module : Blish_HUD.Modules.Module
    {

        private static readonly Logger Logger = Logger.GetLogger<Module>();

        //Audio Player stuff
        private IWaveSource _audioFile;
        private WasapiOut _outputDevice;

        //Settings (maybe one day)
        private SettingEntry<float> _masterVolume;

        internal static Module ModuleInstance;

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        protected override void DefineSettings(SettingCollection settings)
        {
            _masterVolume = settings.DefineSetting("MasterVolume.", 50.0f, "Master Volume", "Is Sebastian a little to loud for you? Well you can attempt to have him sing a little less enthusiastically.");
        }

        protected override void Initialize()
        {

        }

        protected override async Task LoadAsync()
        {
            //Load Loop
            _audioFile = new Mp3MediafoundationDecoder(ContentsManager.GetFileStream("uts_loop4.mp3")).Loop();
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            //Start playing the music at 0 volume
            _outputDevice = new WasapiOut();
            _outputDevice.Initialize(_audioFile);
            _outputDevice.Volume = 0;
            _outputDevice.Play();

            //Catch when the games is closed and started to bring on the music
            GameService.GameIntegration.Gw2Closed += GameIntegration_Gw2Closed;
            GameService.GameIntegration.Gw2Started += GameIntegration_Gw2Started;

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        private void GameIntegration_Gw2Started(object sender, EventArgs e)
        {
            _outputDevice.Play();
        }

        private void GameIntegration_Gw2Closed(object sender, EventArgs e)
        {
            _outputDevice.Stop();
        }

        protected override void Update(GameTime gameTime)
        {
            //REMEMBER: Blish crossed his wires, Z=Y and Y=Z
            //For BlishOS use Zloc
            float Zloc = GameService.Gw2Mumble.PlayerCharacter.Position.Z;
            float volume;

            if (GameService.GameIntegration.IsInGame == false)
            {
                //If UITick is not moving might be loading or some other "state"
                volume = 0;
            }
            else if (Zloc <= 0)
            {
                //Dey unda the sea, LET THE BLOWFISH BLOW 
                volume = Map(Zloc, -30, 0, (_masterVolume.Value / 100), 0.01f);
            }
            else
            {
                //Getting "near" the sea, give 'em a sample of undersea life
                volume = Map(Zloc, 0, 3, 0.01f, 0f);
            }

            //Lets not get crazy here, keep it between 0 and 1
            volume = Clamp(volume, 0, 1);

            //Set the volume
            _outputDevice.Volume = volume;
        }

        private static float Map(float value, float fromLow, float fromHigh, float toLow, float toHigh)
        {
            return (value - fromLow) * (toHigh - toLow) / (fromHigh - fromLow) + toLow;
        }

        public static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        /// <inheritdoc />
        protected override void Unload()
        {
            // Unload
            _outputDevice.Stop();
            _outputDevice.Dispose();
            _outputDevice = null;
            _audioFile.Dispose();
            _audioFile = null;

            // All static members must be manually unset
            ModuleInstance = null;
        }

    }

}
