using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;

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
            _skyLakes = LoadSkyLakesFromJson();

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

        protected override void Unload()
        {
            // Unload
            _soundClip?.Stop();
            _soundClip?.Dispose();
            _skyLakes?.Clear();
        }

        public List<SkyLake> LoadSkyLakesFromJson()
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new Vector3Converter());
            using (StreamReader stream = new StreamReader(ContentsManager.GetFileStream("SkyLakes.json")))
            using (JsonReader reader = new JsonTextReader(stream))
            {
                return serializer.Deserialize<List<SkyLake>>(reader);
            }
        }
    }
}
