﻿using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using OBSWebsocketDotNet.Types;
using StreamNodeEngine.Objects;
using StreamNodeEngine.Utils;
using StreamNodeEngine.Engine.Services;
using StreamNodeEngine.Engine.Services.Obs;
using StreamNodeEngine.Engine.Services.WebSocket;

namespace StreamNodeEngine
{
    public class StreamNodeSocketManager
    {

        public bool isConnected {get {return _isConnected;}}
        private bool _isConnected = false;
        public OBSService obsService { get; } = new OBSService();
        public AudioService audioService { get; } = new AudioService();
        public StoreService storeService { get; } = new StoreService();
        public RemoteControlService webSocketEngine { get; } = new RemoteControlService();

        public StreamNodeSocketManager()
        {
            initRoutes();
        }

        public void ConfigOBSWebSocket(IObsSettings settings) {
            obsService.settings = settings;
        }

        public void ConfigWebSocket(IWebSocketSettings settings) {
            webSocketEngine.settings = settings;
        }

        public void Connect() {
            webSocketEngine.Connect();
            obsService.Connect();
            _isConnected = true;
        }

        public void Disconnect() {
            webSocketEngine.Disconnect();
            _isConnected = false;
        }

        private void initRoutes()
        {
            audioService.OnIOUpdate += IOUpdateHandler;

            webSocketEngine.AddRoute(RemoteControlDataType.Volumes, GetVolumes);
            webSocketEngine.AddRoute(RemoteControlDataType.ChangeVolume, ChangeVolume);
            webSocketEngine.AddRoute(RemoteControlDataType.Obs, GetObs);
            webSocketEngine.AddRoute(RemoteControlDataType.ChangeObs, ChangeObs);
            webSocketEngine.AddRoute(RemoteControlDataType.StoreDeck, StoreDeck);

        }

        private void IOUpdateHandler(object sender, AudioServiceUpdate message)
        {
            LogRedirector.info("Volumes changed");
            RemoteControlData WSData = new RemoteControlData();
            WSData.data = message.volumes;
            WSData.type = RemoteControlDataType.VolumeUpdate;

            webSocketEngine.SendData(WSData);
        }


        private RemoteControlData StoreDeck(RemoteControlData wsData)
        {
            storeService.store(wsData.data);
            RemoteControlData WSDataResponse = new RemoteControlData();
            WSDataResponse.type = RemoteControlDataType.StoreDeck;
            WSDataResponse.status = "OK";
            return WSDataResponse;
        }

        private RemoteControlData GetVolumes(RemoteControlData wsData)
        {
            RemoteControlVolumes volumes = new RemoteControlVolumes();
            VolumeStoreData[] orderVolumes = storeService.read<VolumeStoreData[]>();
            foreach (MMDevice dev in audioService.GetListOfOutputDevices())
            {
                foreach (ApplicationController appOut in audioService.GetApplicationsMixer(dev))
                {
                    ApplicationController appDev = audioService.GetDeviceController(dev);
                    RemoteControlVolume audio = new RemoteControlVolume();
                    audio.name = appOut.processName;
                    audio.volume = (int)(appOut.getVolume() * 100);
                    audio.mute = appOut.getMute();
                    audio.device = appDev.device.FriendlyName;
                    audio.output = true;
                    audio.icon = ProcessUtils.ProcessIcon(appOut.session.GetProcessID);
                    audio.id = audio.name + "|" + audio.device;
                    audio.order = GetVolumeOrder(orderVolumes, audio.device + audio.name);
                    audio.hidden = GetVolumeHidden(orderVolumes, audio.device + audio.name);
                    volumes.Add(audio);
                }
            }

            foreach (MMDevice dev in audioService.GetListOfInputDevices())
            {
                RemoteControlVolume audio = new RemoteControlVolume();

                audio.name = dev.FriendlyName;
                audio.mute = dev.AudioEndpointVolume.Mute;
                audio.volume = (int)(dev.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                audio.device = dev.FriendlyName;
                audio.output = false;
                audio.order = GetVolumeOrder(orderVolumes, audio.device + audio.name);
                volumes.Add(audio);
            }

            wsData.data = volumes;

            return wsData;
        }

        private int GetVolumeOrder(VolumeStoreData[] data, string id) {
            int order = -1;

            if (data != null)
            {
                foreach (VolumeStoreData volume in data)
                {
                    if (volume.id.Equals(id)) order = volume.order;
                }
            }

            return order;
        }

        private bool GetVolumeHidden(VolumeStoreData[] data, string id) {
            bool hidden = false;

            if (data != null)
            {
                foreach (VolumeStoreData volume in data)
                {
                    if (volume.id.Equals(id)) hidden = volume.hidden;
                }
            }

            return hidden;
        }

        private RemoteControlData ChangeVolume(RemoteControlData wsData)
        {
            // TODO Verify input data to avoid exception when unknown data is passed
            ChangeVolumeType ChangeVolume = JsonConvert.DeserializeObject<ChangeVolumeType>(wsData.data.ToString());
            if (ChangeVolume.output)
            {
                ApplicationController app = audioService.GetApplicationOutput(ChangeVolume.name, ChangeVolume.device);
                app.updateVolume((float)ChangeVolume.volume / 100);
                app.setMute(ChangeVolume.mute);
            }
            else
            {
                MMDevice mic = audioService.GetDeviceInput(ChangeVolume.device);
                mic.AudioEndpointVolume.MasterVolumeLevelScalar = (float)ChangeVolume.volume / 100;
                mic.AudioEndpointVolume.Mute = ChangeVolume.mute;
            }

            return wsData;
        }

        private RemoteControlData GetObs(RemoteControlData wsData)
        {
            RemoteObsScenes remoteScenes = new RemoteObsScenes();
            if (obsService.isConnected)
            {
                GetSceneListInfo scenes = obsService.getScenes();
                foreach (OBSScene scene in scenes.Scenes)
                {
                    RemoteObsScene remoteScene = new RemoteObsScene();
                    remoteScene.name = scene.Name;
                    remoteScenes.Add(remoteScene);
                }
            }
            else
            {
                wsData.status = "OBS_WEBSOCKET_ERROR";
            }

            wsData.data = remoteScenes;
            return wsData;
        }

        private RemoteControlData ChangeObs(RemoteControlData wsData)
        {
            if (obsService.isConnected)
            {
                RemoteObsScene ObsScene = JsonConvert.DeserializeObject<RemoteObsScene>(wsData.data.ToString());
                obsService.setCurrentScene(ObsScene.name);
            }
            return wsData;
        }
    }
}
