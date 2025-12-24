using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerHUDCanvas : MonoBehaviour
{
    public TDSCharacterController Controller { get; set; }

    [SerializeReference]
    private TMP_Text healthText;
    [SerializeReference]
    private TMP_Text weaponText;

    public void InitializeCanvas(TDSCharacterController controller)
    {
        this.gameObject.SetActive(true);
        this.Controller = controller;

        this.SetHealth(this.Controller.CurrentHP, this.Controller.MaximumHP);
        this.SetWeapon(controller.OwnWeaponCollection.GetCurrentWeapon());

        this.Controller.HealthChanged += HealthUpdated;
        controller.OwnWeaponCollection.OnChangedToWeapon += SetWeapon;
    }

    void HealthUpdated(double previousHealth, double newHealth, double maxHealth)
    {
        this.SetHealth(newHealth, maxHealth);
    }

    void SetHealth(double toHealth, double maxHealth)
    {
        this.healthText.text = $"{toHealth} / {maxHealth}";
    }

    void SetWeapon(Weapon onWeapon)
    {
        this.weaponText.text = this.Controller.OwnWeaponCollection.GetWeaponCollectionString();
    }
}
