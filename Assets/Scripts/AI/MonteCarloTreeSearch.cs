using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AI
{
    public class MonteCarloTreeSearch : AIBase
    {
        public int iterations = 250;
        public int maxDepth = 15;
        public int maxBranches = 6;
        // larger scalar will increase exploitation, smaller will increase exploration. 
        private static readonly float Scalar = 1 / Mathf.Sqrt(2);
        
        public override Queue<ActionArguments> Plan()
        {
            // accept old plan as base?
            var root = new Node(new AgentState(agent.state) {isNotSimulated = false}) {actionArguments = new ActionArguments()};

            var zoneOfInfluence = new List<Cell>();
            switch (agent)
            {
                case Traveller traveller:
                    root.actionArguments.cell = traveller.occupiedCell;
                    zoneOfInfluence = new List<Cell> {traveller.occupiedCell};
                    break;
                case City city:
                    zoneOfInfluence = city.zoneOfInfluence.ToList();
                    break;
            }
            
            //while resources_left(time, computational power):
            var bestChild = Search(root, zoneOfInfluence);
            var bestPlanReversed = new List<ActionArguments>();
            while (bestChild.parent != null)
            {
                bestPlanReversed.Add(bestChild.actionArguments);
                bestChild = bestChild.parent;
            }
            bestPlanReversed.Reverse();
            var bestPlanQueue = new Queue<ActionArguments>(bestPlanReversed);
            return bestPlanQueue;
        }

        private Node Search(Node root, List<Cell> zoneOfInfluence)
        {
            for (var i = 0; i < iterations; i++)
            {
                if (root.state.isTraveller) zoneOfInfluence[0] = root.actionArguments.cell;
                var frontier = Rollout(root, zoneOfInfluence);
                var reward = Simulate(frontier, zoneOfInfluence);
                BackPropagate(frontier, reward);
            }

            return GetBestChild(root, 0);
        }
        
        private Node Rollout(Node root, List<Cell> zoneOfInfluence)
        {
            // hack to force 'exploitation' where there are many options, might not want to fully expand first
            var node = root;
            while (!node.isTerminal)
            {
                if (node.children.Count == 0) return Expand(node, zoneOfInfluence);
                if (Random.value < 0.05f)
                    node = GetBestChild(node, Scalar);
                else
                {
                    if (!node.IsFullyExpanded(maxBranches)) return Expand(node, zoneOfInfluence);
                    node = GetBestChild(node, Scalar);
                }
            }

            return node;
        }
        private Node Expand(Node node, List<Cell> zoneOfInfluence)
        {
            var triedChildren = node.children.Select(child => child.actionArguments).ToList();
            AgentState newState;
            ActionArguments actionArguments;
            do
            {
                newState = new AgentState(node.state);
                actionArguments = IterateAction(newState, zoneOfInfluence); // or send in the last actionArguments
            } while (actionArguments.time < 0 ||
                     triedChildren.Any(child => 
                         child.action == actionArguments.action && child.amount == actionArguments.amount && 
                         (child.recipe.data == actionArguments.recipe.data && child.ware?.Data == actionArguments.ware?.Data))); // todo test

            newState = new AgentState(newState);
            var isTerminal = !AfterEveryAction(newState, actionArguments.time, zoneOfInfluence) ||
                             node.depth + 1 >= maxDepth;
            
            return node.AddChild(newState, actionArguments, isTerminal);
        }

        private float Simulate(Node root, List<Cell> zoneOfInfluence)
        {
            var state = new AgentState(root.state);
            var depth = root.depth;
            while (depth < maxDepth)//!node.isTerminal)
            {
                ActionArguments actionArguments;
                do
                {
                    actionArguments = IterateAction(state, zoneOfInfluence); // or send in the last actionArguments
                } while (actionArguments.time < 0);
                depth++;

                if (actionArguments.action == Action.Travel) zoneOfInfluence[0] = actionArguments.cell;

                //state = new AgentState(state);
                if (!AfterEveryAction(state, actionArguments.time, zoneOfInfluence))
                    break;
            }

            return Fitness(state, depth);
        }

        // the most vanilla MCTS formula, worth experimenting with THRESHOLD ASCENT (TAGS)
        private Node GetBestChild(Node node, float scalar)
        {
            var bestScore = MinimumFitness;
            var bestChildren = new List<Node>();
            foreach (var child in node.children)
            {
                var exploit = child.reward / child.visits;
                var explore = Mathf.Sqrt(2f * Mathf.Log(node.visits) / child.visits);
                var score = exploit + scalar * explore;
                if (score == bestScore) bestChildren.Add(child);
                else if (score > bestScore)
                {
                    bestChildren = new List<Node> { child };
                    bestScore = score;
                }
                //Debug.Log(bestScore + " " + score);
            }
            
            if (bestChildren.Count == 0) Debug.LogWarning("0 kids! at depth " + node.depth);

            return bestChildren[Random.Range(0, bestChildren.Count)];
        }

        private void BackPropagate(Node node, float reward)
        {
            while (node != null)
            {
                node.visits++;
                node.reward += reward;
                node = node.parent;
            }
        }

        private float Fitness(AgentState state, int depth)
        {
            float fitness;
            if (state.Health == 0 || state.Population < 1)
            {
                fitness = -400;
            }
            else
            {
                var testState = new AgentState(state);
                testState.DecayWares(30);
                testState.DecayNeeds(30);
                fitness = state.Health + state.happiness + (testState.CalculateHealth() + testState.CalculateHappiness()) * 1.5f;  // todo multiply by population?   
            }
            return fitness * Mathf.Pow((maxDepth - 1f * depth) / maxDepth, 2);
        }

        internal class Node
        {
            public int visits;
            //public float reward; // fitness
            public readonly int depth;
            public float reward;
            public AgentState state;
            public ActionArguments actionArguments;
            public readonly List<Node> children = new List<Node>();
            public readonly Node parent;

            public bool isTerminal; //=> depth >= MaxDepth || state.IsTerminal;

            public Node(AgentState state, Node parent = null, int depth = 0, bool isTerminal = false)
            {
                this.state = state; // new AgentState(state)?
                this.parent = parent;
                this.depth = depth;
                //Debug.Log(depth);
                visits = 1;
                this.isTerminal = isTerminal;
                //if (isTerminal || depth >= MaxDepth) ;
                //fitness = 
            }

            public Node AddChild(AgentState childState, ActionArguments childActionArguments, bool isChildTerminal = false)
            {
                var child = new Node(childState, this, depth + 1, isChildTerminal);
                /*if (isChildTerminal) terminals++;
                childrens++;*/
                child.actionArguments = childActionArguments;
                children.Add(child);
                return child;
            }

            public bool IsFullyExpanded(int maxBranches) => children.Count >= maxBranches;
        } 
    }
}
