using UnityEngine;

public class HealPickup : MonoBehaviour, IPickup
{
    [SerializeField]
    public double HealingAmount = 2;
    [SerializeField]
    private int spawnTickets = 1;

    bool isActive { get; set; } = true;

    public void ApplyToCharacter(TDSCharacterController toApply)
    {
        if (!isActive)
        {
            return;
        }

        this.isActive = false;
        toApply.TakeHeal(this.HealingAmount);

        Destroy(this.gameObject);
    }

    public IPickup InstantiateInstance(Vector3 position)
    {
        return GameObject.Instantiate(this, position, Quaternion.identity);
    }

    public int GetSpawnTickets()
    {
        return this.spawnTickets;
    }

    public bool CanTake(TDSCharacterController character)
    {
        return character.CurrentHP < character.MaximumHP;
    }
}
