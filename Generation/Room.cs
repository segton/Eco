using System.Collections.Generic;
using UnityEngine;

public enum RoomType { Generic, Start, Treasure, Boss, Objective }

public class Room : MonoBehaviour
{
    [Tooltip("List of door transforms in this room.")]
    public List<Transform> doorPoints = new List<Transform>();

    [Tooltip("Optional metadata: room type.")]
    public RoomType roomType = RoomType.Generic;
}
