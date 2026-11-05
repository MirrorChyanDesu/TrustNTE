namespace TrustNTE.Models.Update
{
    public enum UpdateTrigger
    {
        Auto,
        Manual
    }

    public enum UpdateChannel
    {
        Stable,
        Alpha
    }

    public sealed class UpdateOption
    {
        public UpdateTrigger Trigger { get; set; } = UpdateTrigger.Auto;
        public UpdateChannel Channel { get; set; } = UpdateChannel.Stable;
    }
}