using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace AI
{
    public class GeneticAlgorithm<T> : AIBase
    {
        public List<DNA<T>> Population { get; private set; }
        public int Generation { get; private set; }
        public float BestFitness { get; private set; }
        public T[] BestGenes { get; }

        public int bias;
        public float mutationRate;

        private List<DNA<T>> newPopulation;
        private float fitnessSum;
        private readonly int dnaSize;
        private readonly Func<T> getRandomGene;
        private readonly Func<int, float> fitnessFunction;

        public GeneticAlgorithm(int populationSize, int dnaSize, Func<T> getRandomGene,
            Func<int, float> fitnessFunction,
            int bias = 5, float mutationRate = 0.01f)
        {
            Generation = 1;
            this.bias = bias;
            this.mutationRate = mutationRate;
            Population = new List<DNA<T>>(populationSize);
            newPopulation = new List<DNA<T>>(populationSize);
            this.dnaSize = dnaSize;
            this.getRandomGene = getRandomGene;
            this.fitnessFunction = fitnessFunction;

            BestGenes = new T[dnaSize];

            for (var i = 0; i < populationSize; i++)
                Population.Add(new DNA<T>(dnaSize, getRandomGene, fitnessFunction));
        }

        public void NewGeneration(int numNewDNA = 0, bool crossoverNewDNA = false)
        {
            // onUpdate() {}
            var finalCount = Population.Count + numNewDNA;

            if (finalCount <= 0) return;

            if (Population.Count > 0)
            {
                CalculateFitness();
                Population.Sort(CompareDNA);
            }

            newPopulation.Clear();

            for (var i = 0; i < Population.Count; i++)
                if (i < bias && i < Population.Count)
                {
                    newPopulation.Add(Population[i]);
                }
                else if (i < Population.Count || crossoverNewDNA)
                {
                    var parent1 = ChooseParent();
                    var parent2 = ChooseParent();

                    var child = parent1.Crossover(parent2);

                    child.Mutate(mutationRate);

                    newPopulation.Add(child);
                }
                else
                {
                    newPopulation.Add(new DNA<T>(dnaSize, getRandomGene, fitnessFunction));
                }

            var tmpList = Population;
            Population = newPopulation;
            newPopulation = tmpList;

            Generation++;
        }

        private int CompareDNA(DNA<T> a, DNA<T> b)
        {
            if (a.Fitness > b.Fitness)
                return -1;
            if (a.Fitness < b.Fitness)
                return 1;
            return 0;
        }

        private void CalculateFitness()
        {
            fitnessSum = 0;
            var best = Population[0];

            for (var i = 0; i < Population.Count; i++)
            {
                fitnessSum += Population[i].CalculateFitness(i);

                if (Population[i].Fitness > best.Fitness) best = Population[i];
            }

            BestFitness = best.Fitness;
            best.Genes.CopyTo(BestGenes, 0);
        }

        private DNA<T> ChooseParent()
        {
            var randomNumber = Random.value * fitnessSum;

            for (var i = 0; i < Population.Count; i++)
            {
                if (randomNumber < Population[i].Fitness) return Population[i];

                randomNumber -= Population[i].Fitness;
            }

            return null;
        }
    }
}