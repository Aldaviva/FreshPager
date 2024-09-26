namespace FreshPager.Data;

public readonly record struct Check(int id, string name) {

    /// <inheritdoc />
    public bool Equals(Check other) => id == other.id && name == other.name;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(id, name);

}