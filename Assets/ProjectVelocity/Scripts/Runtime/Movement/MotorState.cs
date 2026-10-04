namespace ProjectVelocity
{
    /// <summary>What the motor is doing right now, for debug readouts (and later animation).</summary>
    public enum MotorState
    {
        Ground,
        Air,
        Boost,
        Wall,
        /// <summary>Being pulled through a traversal target.</summary>
        Target,
    }
}
