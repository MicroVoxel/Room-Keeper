using UnityEngine;

public class Connector : MonoBehaviour
{
    [SerializeField] private bool isOccupied = false;

    public void SetOccupied(bool value = true) => isOccupied = value;
    public bool IsOccupied() => isOccupied;
    public void Use() => isOccupied = true;
    public void Unuse() => isOccupied = false;

}
