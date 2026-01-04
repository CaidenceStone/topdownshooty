using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponCollection : MonoBehaviour
{
    public WeaponCollectionData Data { get; set; } = new WeaponCollectionData();

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
        this.Data.WeaponPFs.Clear();

        Debug.Log($"Initializing weapon collection with {this.Weapons.Count} weapons");
        foreach (Weapon curWeapon in this.Weapons)
        {
            curWeapon.InitializeWeapon(this.entity.MyFaction, owningEntity);
            curWeapon.SetIsInWorld(false);

            // Create a prefab that will be hidden away as don't be destroyed
            // that way it can be used to repopulate the character's weapons
            Debug.Log($"Creating psuedo weapon pf in {curWeapon.WeaponName}");
            Weapon newWeaponPrefab = Instantiate(curWeapon);
            DontDestroyOnLoad(newWeaponPrefab);
            newWeaponPrefab.gameObject.SetActive(false);
            this.Data.WeaponPFs.Add(newWeaponPrefab);
        }
    }

    public void InitializeWeaponCollection(Entity owningEntity, WeaponCollectionData preexistingData)
    {
        this.Data = preexistingData;
        this.entity = owningEntity;

        for (int ii = this.Weapons.Count - 1; ii >= 0; ii--)
        {
            Destroy(this.Weapons[ii].gameObject);
        }
        this.Weapons.Clear();

        Debug.Log($"Loaded with {this.Data.WeaponPFs.Count} weapons");
        foreach (Weapon curWeapon in this.Data.WeaponPFs)
        {
            Weapon newWeapon = Instantiate(curWeapon, this.transform, false);
            newWeapon.InitializeWeapon(this.entity.MyFaction, this.entity);
            newWeapon.SetIsInWorld(false);
            this.Weapons.Add(newWeapon);
            newWeapon.gameObject.SetActive(true);
            curWeapon.TimeBetweenShots = Mathf.Lerp(curWeapon.TimeBetweenShots, 0, owningEntity.Modifiers.ReloadSpeedPercentageReductionModifier);
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

    public void AddWeapon(Weapon toAdd)
    {
        // Create a prefab that will be hidden away as don't be destroyed
        // that way it can be used to repopulate the character's weapons
        Weapon newWeaponPrefab = Instantiate(toAdd);
        DontDestroyOnLoad(newWeaponPrefab);
        newWeaponPrefab.gameObject.SetActive(false);
        this.Data.WeaponPFs.Add(newWeaponPrefab);

        Debug.Log($"Creating psuedo weapon pf in {toAdd.WeaponName}");
        this.Weapons.Add(toAdd);
        toAdd.SetIsInWorld(false);
        toAdd.transform.SetParent(this.transform, false);
        toAdd.transform.localPosition = Vector3.zero;
        toAdd.InitializeWeapon(this.entity.MyFaction, this.entity);
        toAdd.TimeBetweenShots = Mathf.Lerp(toAdd.TimeBetweenShots, 0, this.entity.Modifiers.ReloadSpeedPercentageReductionModifier);
        this.OnChangedToWeapon?.Invoke(this.GetCurrentWeapon());
    }
}
