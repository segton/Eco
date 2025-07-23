using Unity.Netcode;
using Unity.Collections;
using System;

public struct BatteryData : INetworkSerializable, IEquatable<BatteryData>
{
    public FixedString64Bytes uniqueItemID;
    public float batteryLevel;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref uniqueItemID);
        serializer.SerializeValue(ref batteryLevel);
    }

    public bool Equals(BatteryData other)
    {
        return uniqueItemID.Equals(other.uniqueItemID) && batteryLevel.Equals(other.batteryLevel);
    }
}
