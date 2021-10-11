using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using NAudio.Wave;

namespace Taimi.UndaDaSea_BlishHUD
{

    [Export(typeof(Blish_HUD.Modules.Module))]
    public class Module : Blish_HUD.Modules.Module
    {

        private static readonly Logger Logger = Logger.GetLogger<Module>();

        private WaveOut _soundClip;

        //Settings
        private SettingEntry<float> _masterVolume;

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
            _masterVolume = settings.DefineSetting("MasterVolume.", 50.0f, () => "Master Volume", () => "Is Sebastian a little to loud for you? Well you can attempt to have him sing a little less enthusiastically.");
        }

        protected override void Initialize()
        {

        }

        protected override async Task LoadAsync()
        {
            var stream = ContentsManager.GetFileStream("uts_loop4.mp3");
            var reader = new LoopingAudioStream(new Mp3FileReader(stream));
            _soundClip = new WaveOut();
            _soundClip.Init(reader);
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            //Start playing the music at 0 volume
            _soundClip.Volume = 0;

            //Catch when the games is closed and started to bring on the music
            GameService.GameIntegration.Gw2Instance.Gw2Closed  += GameIntegration_Gw2Closed;
            GameService.GameIntegration.Gw2Instance.Gw2Started += GameIntegration_Gw2Started;

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        private void GameIntegration_Gw2Started(object sender, EventArgs e)
        {
            _soundClip.Play();
        }

        private void GameIntegration_Gw2Closed(object sender, EventArgs e)
        {
            _soundClip.Stop();
        }

        private double _timeSinceUpdate;

        private void UpdateVolume(GameTime gameTime)
        {
            // Expensive to set the volume
            if (_timeSinceUpdate < 300)
            {
                _timeSinceUpdate += gameTime.ElapsedGameTime.TotalMilliseconds;
                return;
            }

            _timeSinceUpdate = 0;

            //REMEMBER: Blish crossed his wires, Z=Y and Y=Z
            //For BlishOS use Zloc
            float Zloc = GameService.Gw2Mumble.PlayerCharacter.Position.Z;
            float volume;

            if (Zloc <= 0) {
                //Dey unda the sea, LET THE BLOWFISH BLOW 
                volume = Map(Zloc, -30, 0, (_masterVolume.Value / 100), 0.01f);
            } else {
                //Getting "near" the sea, give 'em a sample of undersea life
                volume = Map(Zloc, 0, 3, 0.01f, 0f);
            }

            //Lets not get crazy here, keep it between 0 and 1
            volume = Clamp(volume, 0, 1);

            //Set the volume
            _soundClip.Volume = volume;
        }

        protected override void Update(GameTime gameTime)
        {
            if (GameService.GameIntegration.Gw2Instance.IsInGame == false)
            {
                //If UITick is not moving might be loading or some other "state" so we pause
                if (_soundClip.PlaybackState == PlaybackState.Playing)
                {
                    _soundClip.Pause();
                }
                return;
            }

            UpdateVolume(gameTime);
            
            if (_soundClip.PlaybackState != PlaybackState.Playing)
            {
                // We reset volume back to 0 to avoid the audio playing for a second when teleporting after being in the water
                _soundClip.Volume = 0f;
                _soundClip.Play();
            }
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
            _soundClip.Stop();
            _soundClip.Dispose();
        }

    }

}
