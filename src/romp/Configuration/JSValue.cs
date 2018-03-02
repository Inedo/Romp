namespace Inedo.Romp.Configuration
{
    internal readonly struct JSValue<T>
    {
        public JSValue(T value)
        {
            this.Value = value;
            this.IsDefined = true;
        }

        public static implicit operator JSValue<T>(T value) => new JSValue<T>(value);

        public T Value { get; }
        public bool IsDefined { get; }

        public JSValue<T> Coalesce(JSValue<T> other, bool coalesceNull)
        {
            if (coalesceNull && this.IsDefined && other.IsDefined)
                return new JSValue<T>(this.Value == null ? other.Value : this.Value);

            return this.IsDefined ? this : other;
        }

        public override string ToString()
        {
            if (!this.IsDefined)
                return "undefined";

            if (this.Value == null)
                return "null";

            return this.Value.ToString();
        }
    }
}
