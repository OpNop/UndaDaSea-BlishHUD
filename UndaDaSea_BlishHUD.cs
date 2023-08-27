using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Taimi.UndaDaSea_BlishHUD
{

    [Export(typeof(Blish_HUD.Modules.Module))]
    public class Module : Blish_HUD.Modules.Module
    {

        private static readonly Logger Logger = Logger.GetLogger<Module>();

        private IWavePlayer _soundClip;

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

        }

        VolumeSampleProvider _volumeSampler;
        List<SkyLake> _skyLakes;
        int[] SkyLakeMaps = {1510, 1517, 1509};
        float waterOffset = 0;

        protected override void OnModuleLoaded(EventArgs e)
        {
            var stream = ContentsManager.GetFileStream("uts_loop4.mp3");
            var reader = new LoopingAudioStream(new Mp3FileReader(stream));

            _volumeSampler = new VolumeSampleProvider(reader.ToSampleProvider());

            _soundClip = new WaveOutEvent();
            _soundClip.Init(_volumeSampler);

            //Start playing the music at 0 volume
            _volumeSampler.Volume = 0f;

            //Catch when the games is closed and started to bring on the music
            GameService.GameIntegration.Gw2Instance.Gw2Closed  += GameIntegration_Gw2Closed;
            GameService.GameIntegration.Gw2Instance.Gw2Started += GameIntegration_Gw2Started;

            //Load SkyLakes
            LoadSkyLakes();

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
            //For BlishOS use Z
            var playerPosition = GameService.Gw2Mumble.PlayerCharacter.Position;
            var CurrentMap = GameService.Gw2Mumble.CurrentMap.Id;
            var volume = 0f;

            //SotO "Sky lakes"
            if (SkyLakeMaps.Contains(CurrentMap))
            {
                //Check if they are near/in a Sky Lake
                var NearbyLake = _skyLakes.Where(lake => lake.IsNearby(playerPosition)).OrderBy(lake => lake.Distance).FirstOrDefault();
                waterOffset = (NearbyLake != null && NearbyLake.IsInWater(playerPosition)) ? NearbyLake.WaterSurface : 0;
            } 
            else
            {
                //Default water level
                waterOffset = 0;
            }

            if (playerPosition.Z <= waterOffset) {
                //Dey unda the sea, LET THE BLOWFISH BLOW 
                volume = Map(playerPosition.Z, waterOffset - 30, waterOffset, (_masterVolume.Value / 100), 0.01f);
            } else {
                //Getting "near" the sea, give 'em a sample of undersea life
                volume = Map(playerPosition.Z, waterOffset, waterOffset + 3, 0.01f, 0f);
            }

            //Lets not get crazy here, keep it between 0 and 1
            volume = Clamp(volume, 0f, 1f);

            //Set the volume
            _volumeSampler.Volume = volume;
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
                _volumeSampler.Volume = 0f;
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
            _soundClip?.Stop();
            _soundClip?.Dispose();
            _skyLakes?.Clear();
        }

        //Lame Sky Lake Data Loader
        private void LoadSkyLakes()
        {
            _skyLakes = new List<SkyLake>
            {
                //Jade Mech Habitation Zone 03
                new SkyLake(396, 300, new List<Vector3> {
                    new Vector3(77,   -53, 402),
                    new Vector3(77,   115, 402),
                    new Vector3(-155, 115, 402),
                    new Vector3(-155, -53, 402),
                }),

                //Stargaze Ridge
                new SkyLake(260, 200, new List<Vector3> {
                    new Vector3(1038, 263, 260),
                    new Vector3(1111, 252, 260),
                    new Vector3(1100, 35,  260),
                    new Vector3(1012, 85,  260),
                }),

                //Primal Maguuma (Low lake)
                new SkyLake(353, 300, new List<Vector3>
                {
                    new Vector3(-83, 584, 353),
                    new Vector3(-81, 626, 353),
                    new Vector3(-24, 634, 353),
                    new Vector3(52, 594, 353),
                    new Vector3(85, 485, 353),
                    new Vector3(195, 467, 353),
                    new Vector3(208, 423, 353),
                    new Vector3(157, 386, 353),
                    new Vector3(152, 317, 353),
                    new Vector3(56, 310, 353),
                    new Vector3(18, 376, 353),
                    new Vector3(22, 416, 353),
                    new Vector3(-20, 438, 353),
                    new Vector3(-29, 538, 353),
                }),

                //Primal Maguuma (mid lake)
                new SkyLake(435, 400, new List<Vector3> {
                    new Vector3(8.762414f, 365.2565f, 435),
                    new Vector3(2.563565f, 408.4081f, 435),
                    new Vector3(-22.61929f, 438.4842f, 435),
                    new Vector3(-45.60424f, 500.1057f, 435),
                    new Vector3(-73.58975f, 559.9127f, 435),
                    new Vector3(-126.0663f, 559.3304f, 435),
                    new Vector3(-125.1464f, 467.7312f, 435),
                    new Vector3(-80.47857f, 398.7868f, 435),
                    new Vector3(-54.03018f, 361.1153f, 435),
                    new Vector3(-6.560186f, 349.9526f, 435),
                }),

                //Primal Maguuma (high lake)
                new SkyLake(484, 430, new List<Vector3> {
                    new Vector3(-121.9899f, 437.0892f, 484),
                    new Vector3(-157.1637f, 462.3725f, 484),
                    new Vector3(-167.7869f, 471.4856f, 484),
                    new Vector3(-170.0892f, 497.2692f, 484),
                    new Vector3(-243.7502f, 511.4792f, 484),
                    new Vector3(-243.0668f, 466.6464f, 484),
                    new Vector3(-221.771f, 439.689f, 484),
                }),

                //Primal Maguuma (top pond)
                new SkyLake(494, 483, new List<Vector3> {
                    new Vector3(-319.1087f, 516.947f, 494),
                    new Vector3(-248.7634f, 511.2591f, 494),
                    new Vector3(-250.345f, 480.397f, 494),
                    new Vector3(-313.2242f, 487.2502f, 494),
                }),
            };
        }

    }

}
