using System;
using Sandbox.ModAPI;

namespace HnzCoopSeason.Utils
{
    public sealed class Interval
    {
        int _lastFrame;

        public void Initialize()
        {
            _lastFrame = MyAPIGateway.Session.GameplayFrameCounter;
        }

        public bool Update(int span)
        {
            var now = MyAPIGateway.Session.GameplayFrameCounter;
            if (now - _lastFrame > span)
            {
                _lastFrame = now;
                return true;
            }

            return false;
        }
    }
}