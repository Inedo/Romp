namespace Inedo.Romp
{
    internal sealed class ArgOption
    {
        public ArgOption(string key, string value)
        {
            this.Key = key;
            this.Value = value;
        }

        public string Key { get; }
        public string Value { get; }
    }
}
