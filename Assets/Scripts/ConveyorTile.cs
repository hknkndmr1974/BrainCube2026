using UnityEngine;

/// <summary>
/// Oyuncunun üstüne geldiğinde onu belirli bir yönde ilerletir.
/// LevelLoader tarafından m0–m3 tokenlarına göre otomatik atanır:
///   m0 = sağ  (+X)
///   m1 = ileri (+Z)
///   m2 = sol   (-X)
///   m3 = geri  (-Z)
/// </summary>
public class ConveyorTile : MonoBehaviour
{
    public Vector3 direction = Vector3.right;
}
