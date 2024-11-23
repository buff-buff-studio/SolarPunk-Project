using System.Collections;
using System.Collections.Generic;
using NetBuff.Misc;
using Solis.Circuit;
using Solis.Packets;
using Solis.Player;
using UnityEngine;

public class CircuitValve : CircuitInteractive
{
     [Header("REFERENCES")]
     public CircuitPlug output;

     [Header("SETTINGS")] 
     [SerializeField]
     private Transform valve;
     [SerializeField]
     private float valveAngle = 60;

     protected override void OnEnable()
     {
         base.OnEnable();
         WithValues(isOn);

         isOn.OnValueChanged += _OnValueChanged;

         valve.localEulerAngles = new Vector3(270, 0, isOn.Value ? valveAngle : 0);
     }

     protected void Update()
     {
         valve.localEulerAngles = Vector3.Lerp(valve.localEulerAngles, new Vector3(270, 0, isOn.Value ? valveAngle : 0), Time.deltaTime * 2);
     }

     public override CircuitData ReadOutput(CircuitPlug plug)
     {
         return new CircuitData(isOn.Value);
     }

     public override IEnumerable<CircuitPlug> GetPlugs()
     {
         yield return output;
     }

     protected override void OnRefresh()
     {
     }

     protected override bool OnPlayerInteract(PlayerInteractPacket arg1, int arg2)
     {
         if (!PlayerChecker(arg1, out var player))
             return false;
         isOn.Value = !isOn.Value;

         player.PlayInteraction(InteractionType.Lever);
         ServerBroadcastPacket(new InteractObjectPacket()
         {
             Id = arg1.Id,
             Interaction = InteractionType.Lever
         });

         return true;
     }
}
