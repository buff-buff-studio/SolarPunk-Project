using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Misc.Props
{
    public abstract class AnimalState
    {
        protected AnimalController animalController;
        protected float timeInState;
        
        public AnimalState(AnimalController animalController)
        {
            this.animalController = animalController;
        }
        
        public abstract void OnEnter();
        public virtual void OnExit(){}

        public virtual void OnUpdate(float deltaTime)
        {
            timeInState += Time.deltaTime;
        }
        public virtual void OnFixedUpdate(){}
        public abstract Vector3 GetWalkInput();
        public abstract override string ToString(); 
    }
    
    public class IdleState : AnimalState
    {
        private float _timeToNextState;
        public IdleState(AnimalController animalController) : base(animalController)
        {
        }
        public override void OnEnter()
        {
            _timeToNextState = Random.Range(2, 6);
        }

        public override void OnExit() 
        {
            throw new NotImplementedException();
        }

        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);

            if (timeInState >= _timeToNextState)
                animalController.SetState(Random.Range(0,3) < 2 ? new WalkingState(animalController) : new IdleState(animalController));
        }

        public override void OnFixedUpdate()
        {
            throw new NotImplementedException();
        }

        public override Vector3 GetWalkInput()
        {
            throw new NotImplementedException();
        }

        public override string ToString() => "Idle";
    }

    public class WalkingState : AnimalState
    {
        private float _walkingRadius;
        private float _checkTime = 0.15f;
        private float _currentTime = 0f;
        
        public WalkingState(AnimalController animalController) : base(animalController)
        {
        }

        public override void OnEnter()
        {
            animalController.SetDestination(GenerateRandomDestination());
        }
        
        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);
            _currentTime += deltaTime;

            if (_currentTime >= _checkTime)
            {
                _currentTime = 0;
                ReachedDestination();
            }
            
            if (timeInState >= 20)
                animalController.SetState(new IdleState(animalController));
            
            
        }

        public override void OnExit()
        {
            throw new NotImplementedException();
        }

        public override Vector3 GetWalkInput()
        {
            throw new NotImplementedException();
        }
        
        public void ReachedDestination()
        {
            if (animalController.agent.pathPending || !(animalController.agent.remainingDistance <= 0.5f) ||
                animalController.agent.remainingDistance == Mathf.Infinity) return;
            
            Debug.Log("Agent has reached the destination.");

            if (Random.Range(0, 5) <= 2)
                animalController.SetState(new IdleState(animalController));
            else
                animalController.SetDestination(GenerateRandomDestination());
        }

        private Vector3 GenerateRandomDestination()
        {
            return NavMesh.SamplePosition(Random.insideUnitCircle * _walkingRadius, out NavMeshHit hit, 1, new NavMeshQueryFilter()) ? hit.position : GenerateRandomDestination();
        }

        public override string ToString() => "Walking";
    }
    
    public class AnimalController : MonoBehaviour
    {
       private AnimalState state;
       public Animator animalAnimator;
       public NavMeshAgent agent;
       private void Start()
       {
           SetState(new IdleState(this));
       }

       private void Update()
       {
           Debug.Log(state.ToString());
           state.OnUpdate(Time.deltaTime);
       }
       
       private void FixedUpdate()
       {
           Debug.Log(state.ToString());
           state.OnFixedUpdate();
       }
       
       public void SetState(AnimalState newState)
       {
           Debug.Log($"Changing state from {state} to {newState}");
           state.OnExit();
           state = newState;
           state.OnEnter();
       }

       public void SetDestination(Vector3 newDestination)
       {
           if(newDestination == Vector3.zero) return;
           agent.SetDestination(newDestination);
       }
    }
}
