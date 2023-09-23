using System;
using Pulsar4X.Engine;
using Pulsar4X.Orbital;

namespace Pulsar4X.Datablobs
{
    public class NewtonSimpleMoveDB : BaseDataBlob
    {
        internal DateTime LastProcessDateTime = new DateTime();
        public DateTime ActionOnDateTime { get; internal set; }
        public KeplerElements CurrentTrajectory { get; internal set; }
        public KeplerElements TargetTrajectory { get; internal set; }

        public bool IsComplete = false;
        public Entity SOIParent { get; internal set; }
        public double ParentMass { get; internal set; }
        public override object Clone()
        {
            throw new NotImplementedException();
        }
    }
}