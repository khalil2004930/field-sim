using System;
using FieldSim.Unity.Core;

namespace FieldSim.Unity.Integration
{
    [Serializable]
    public sealed class EnvironmentBridgeRequest
    {
        public string id;
        public string type;
        public FieldSimPosition point;
        public FieldSimPosition from;
        public FieldSimPosition to;
        public string unitClass;
    }

    [Serializable]
    public sealed class EnvironmentBridgeResponse
    {
        public string id;
        public bool handled;
        public bool boolValue;
        public double numberValue;
        public string status;
        public string textValue;
        public bool hasPoint;
        public FieldSimPosition point;
        public FieldSimPosition[] waypoints;
    }
}
