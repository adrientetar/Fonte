﻿
namespace Fonte.Data.Changes
{
    using Fonte.Data.Interfaces;

    internal struct PointXChange : IChange
    {
        private readonly Point _target;
        private float _value;

        public bool AffectsSelection => false;
        public bool IsShallow => false;

        public PointXChange(Point target, float value)
        {
            _target = target;
            _value = value;
        }

        public void Apply()
        {
            var oldValue = _target._x;
            _target._x = _value;
            _value = oldValue;

            _target.Parent?.OnChange(this);
        }
    }
}
