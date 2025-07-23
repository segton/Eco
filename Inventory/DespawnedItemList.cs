using Unity.Collections;
using Unity.Netcode;
using System.Collections.Generic;

public struct DespawnedItemsList : INetworkSerializable
{
    public List<ulong> itemIds;

    // Ensure list is initialized
    public DespawnedItemsList(List<ulong> items)
    {
        itemIds = items ?? new List<ulong>();
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsWriter)
        {
            int count = itemIds.Count;
            serializer.SerializeValue(ref count);

            for (int i = 0; i < count; i++)
            {
                ulong id = itemIds[i];
                serializer.SerializeValue(ref id);
            }
        }
        else
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            itemIds = new List<ulong>(count);

            for (int i = 0; i < count; i++)
            {
                ulong id = 0;
                serializer.SerializeValue(ref id);
                itemIds.Add(id);
            }
        }
    }
}
