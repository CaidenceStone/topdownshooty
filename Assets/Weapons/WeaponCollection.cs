using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class WeaponCollection : MonoBehaviour
{
    [SerializeField]
    public List<Weapon> Weapons = new List<Weapon>();
    private int currentWeaponIndex { get; set; } = 0;
    [SerializeReference]
    private Entity entity;

    public delegate void OnChangedToWeaponDelegate(Weapon newWeapon);
    public event OnChangedToWeaponDelegate OnChangedToWeapon;

    public void InitializeWeaponCollection(Entity owningEntity)
    {
        this.entity = owningEntity;

        foreach (Weapon curWeapon in this.Weapons)
        {
            curWeapon.InitializeWeapon(this.entity.MyFaction);
        }
    }

    public void TickDownTimers()
    {
        foreach (Weapon curWeapon in this.Weapons)
        {
            curWeapon.TickDownTimers();
        }
    }

    public Weapon GetCurrentWeapon()
    {
        if (this.Weapons.Count == 0)
        {
            return null;
        }

        return this.Weapons[this.currentWeaponIndex];
    }

    public void CycleWeapons(int direction)
    {
        this.currentWeaponIndex = (this.currentWeaponIndex + direction + this.Weapons.Count) % this.Weapons.Count;
        this.OnChangedToWeapon?.Invoke(this.GetCurrentWeapon());
        Debug.Log($"Swapping to {this.GetCurrentWeapon().WeaponName}");
    }

    public string GetWeaponCollectionString()
    {
        StringBuilder collectionString = new StringBuilder();
        string comma = "";

        for (int ii = 0; ii < this.Weapons.Count; ii++)
        {
            collectionString.Append(comma);
            if (ii == this.currentWeaponIndex)
            {
                collectionString.Append("[<b>");
            }
            collectionString.Append(this.Weapons[ii].WeaponName);
            if (ii == this.currentWeaponIndex)
            {
                collectionString.Append("</b>]");
            }
            comma = ", ";
        }

        return collectionString.ToString();
    }
}
