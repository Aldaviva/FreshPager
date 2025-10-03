namespace FreshPager.Data;

public readonly record struct FreshpingCheck(int id, string name) {

    /// <inheritdoc />
    public bool Equals(FreshpingCheck other) => id == other.id && name == other.name;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(id, name);

}