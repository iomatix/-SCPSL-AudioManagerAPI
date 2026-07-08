namespace AudioManagerAPI.Features.Management.Settings
{
    /// <summary>
    /// Encapsulates the configuration profile for trigonometric audio orbital movement.
    /// </summary>
    public struct OrbitSettings
    {
        public float MaxRadius;
        public float MinRadius;
        public float AngularSpeed;
        public float ApproachSpeed;
        public float HeightOffset;

        public OrbitSettings(float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, float heightOffset = 0.85f)
        {
            MaxRadius = maxRadius;
            MinRadius = minRadius;
            AngularSpeed = angularSpeed;
            ApproachSpeed = approachSpeed;
            HeightOffset = heightOffset;
        }
    }
}
