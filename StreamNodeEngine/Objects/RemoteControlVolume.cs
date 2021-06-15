﻿namespace StreamNodeEngine.Objects
{
    public class RemoteControlVolume
    {
        public string id { get; set; }
        public string name { get; set; }
        public int volume { get; set; } = -1;
        public bool output { get; set; }
        public bool mute { get; set; }
        public string icon { get; set; }
        public string device { get; set; }
        public int order { get; set; } = -1;
        public bool hidden {get; set;} = false;

    }
}
