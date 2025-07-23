using Unity.Collections;
using Unity.Netcode;
using System;

public struct ItemData : INetworkSerializable, IEquatable<ItemData>
{
    public FixedString64Bytes itemID;
    public FixedString64Bytes itemName;
    // Dynamic fields for battery data:
    public FixedString64Bytes uniqueItemID;
    public float batteryLevel; // Current battery charge
    // New field: tracks the last owner (client ID) of the item.
    public bool isOn;
    public ulong ownerId;

    public bool IsEmpty => itemID.Length == 0;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemID);
        serializer.SerializeValue(ref itemName);
        serializer.SerializeValue(ref uniqueItemID);
        serializer.SerializeValue(ref batteryLevel);
        serializer.SerializeValue(ref ownerId);
        serializer.SerializeValue(ref isOn);
    }

    public bool Equals(ItemData other)
    {
        return itemID.Equals(other.itemID) &&
               itemName.Equals(other.itemName) &&
               uniqueItemID.Equals(other.uniqueItemID) &&
               batteryLevel.Equals(other.batteryLevel) &&
               ownerId.Equals(other.ownerId) &&
               isOn == other.isOn;

    }

    public override bool Equals(object obj) => obj is ItemData other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(itemID, itemName, uniqueItemID, batteryLevel, ownerId, isOn);
    public static bool operator ==(ItemData left, ItemData right) => left.Equals(right);
    public static bool operator !=(ItemData left, ItemData right) => !left.Equals(right);
}
