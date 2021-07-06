using System;
using System.Collections.Generic;
using System.Linq;
using ScriptableObjects;
using UnityEngine;
using WorldGen;
using Random = UnityEngine.Random;

namespace AI
{
    public class Greedy : AIBase
    {
        //private readonly List<Func<Ware, float, float>> actions = new List<Func<Ware, float, float>>();

        public int numberOfActions = 7;
        public int numberOfIterations = 250;

        public AgentState state;

        public List<Cell> baseZoneOfInfluence;

        public List<Cell> zoneOfInfluence;

        public override Queue<ActionArguments> Plan()
        {
            var bestPlan = new Queue<ActionArguments>();
            var bestFitness = MinimumFitness;

            zoneOfInfluence = agent switch
            {
                Traveller traveller => new List<Cell> {traveller.occupiedCell},
                City city => city.zoneOfInfluence.ToList(),
                _ => zoneOfInfluence
            };

            var actionsPickNumber = new int[5];
            for (var i = 0; i < numberOfIterations; i++)
            {
                state = new AgentState(agent.state) {isNotSimulated = false};
                zoneOfInfluence = baseZoneOfInfluence;
                var plan = new Queue<ActionArguments>();
                for (var a = 0; a < numberOfActions; a++)
                {
                    var actionArguments = IterateAction(state, zoneOfInfluence);
                    
                    if (actionArguments.time < 0)
                    {
                        a--;
                        continue;
                    }
                    
                    if (actionArguments.action == Action.Travel) zoneOfInfluence[0] = actionArguments.cell;
                    
                    plan.Enqueue(actionArguments);
                    
                    state = new AgentState(state);
                    if (!AfterEveryAction(state, actionArguments.time, zoneOfInfluence))
                    {
                        //Debug.Log("dead " + state.Health + " " + state.happiness + " " + state.Population);
                        break;
                    }
                }

                var fitness = Fitness(state);
                if (fitness > bestFitness)
                {
                    bestFitness = fitness;
                    bestPlan = plan;
                }
            }

            var s = "";
            foreach (var need in state.needs)
            {
                s += need.Value + " " ;
            }
            //Debug.Log(s);
                

            var s2 = "";
            foreach (var a in actionsPickNumber)
            {
                s2 += a + " " ;
            }
            //Debug.Log(s2);
                
            //Debug.Log(produceFail);
            
            //agent.actionSequence = 
            //Debug.Log(finalSequence);
            //Debug.Log(bestFitness + " " + bestHappiness + " " + bestHealth);
            return bestPlan;
        }

        public float Fitness(AgentState agentState)
        {
            if (agentState.Health == 0) return -1000;
            var testState = new AgentState(agentState);
            testState.DecayWares(15);
            testState.DecayNeeds(15);
            return agentState.Health + agentState.happiness + (agentState.CalculateHealth() + testState.CalculateHappiness()) * 1.5f; // todo multiply by population?
        }
    }
}
