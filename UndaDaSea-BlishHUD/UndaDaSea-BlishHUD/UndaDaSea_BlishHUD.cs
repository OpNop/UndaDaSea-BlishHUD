﻿using System;
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

        private static readonly Logger Logger = Logger.GetLogger(typeof(Module));

        internal static Module ModuleInstance;

        //Audio Player stuff
        private StreamMediaFoundationReader _audioFile;
        private WaveOutEvent _outputDevice;
        private LoopStream _loop;

        //Settings (maybe one day)
        private SettingEntry<float> _masterVolume;

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { ModuleInstance = this; }

        protected override void DefineSettings(SettingCollection settings)
        {
            _masterVolume = settings.DefineSetting("MasterVolume.", 1.0f, "Master Volume", "Is Sebastian a little to loud for you? Well you can attempt to have him sing a little less enthusiastically.");
        }

        protected override void Initialize()
        {

        }

        protected override async Task LoadAsync()
        {
            //Load Loop
            _audioFile = new StreamMediaFoundationReader(ContentsManager.GetFileStream("uts_loop4.mp3"));
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            //Start playing the music at 0 volume
            _loop = new LoopStream(_audioFile);
            _outputDevice = new WaveOutEvent();
            _outputDevice.Init(_loop);
            _outputDevice.Volume = 0;
            _outputDevice.Play();

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        protected override void Update(GameTime gameTime)
        {
            //REMEMBER: Blish crossed his wires, Z=Y and Y=Z
            //For BlishOS use Zloc
            float Zloc = GameService.Gw2Mumble.PlayerCharacter.Position.Z;
            float volume;

            if (Zloc <= 0)
            {
                volume = Map(Zloc, -30, 0, _masterVolume.Value, 0.01f);
            }
            else
            {
                volume = Map(Zloc, 0, 3, 0.01f, 0f);
            }

            //Lets not get crazy here, keep it between 0 and 1
            volume = Clamp(volume, 0, 1);

            //Set the volume
            _outputDevice.Volume = volume;
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
            _loop.Dispose();
            _loop = null;

            // All static members must be manually unset
            ModuleInstance = null;
        }

        private static float Map(float value, float fromLow, float fromHigh, float toLow, float toHigh)
        {
            return (value - fromLow) * (toHigh - toLow) / (fromHigh - fromLow) + toLow;
        }

        public static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

    }

}