using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class Connector : MonoBehaviour
{
    [SerializeField] private bool isOccupied = false;
    
    public void SetOccupied(bool value = true) => isOccupied = value;

    public bool IsOccupied() => isOccupied;
}
