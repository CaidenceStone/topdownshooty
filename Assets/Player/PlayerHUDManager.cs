using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHUDManager : MonoBehaviour
{
    [SerializeField]
    private List<PlayerHUDCanvas> huds = new List<PlayerHUDCanvas>();
    private int assignedHUDS { get; set; } = 0;

    private void Awake()
    {
        foreach (PlayerHUDCanvas canvas in huds)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    public bool TryRegisterCanvas(TDSCharacterController character, out PlayerHUDCanvas assignedCanvas)
    {
        if (this.assignedHUDS > this.huds.Count)
        {
            assignedCanvas = null;
            return false;
        }

        assignedCanvas = huds[assignedHUDS];
        assignedCanvas.InitializeCanvas(character);
        assignedHUDS++;
        return true;
    }
}
