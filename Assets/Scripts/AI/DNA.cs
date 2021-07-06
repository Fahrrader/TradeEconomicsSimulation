using System;
using Random = UnityEngine.Random;

namespace AI
{
    public class DNA<T>
    {
        public T[] Genes { get; }
        public float Fitness { get; private set; }

        private readonly Func<T> getRandomGene;
        private readonly Func<int, float> fitnessFunction;

        public DNA(int size, Func<T> getRandomGene, Func<int, float> fitnessFunction, bool shouldInitGenes = true)
        {
            Genes = new T[size];
            this.getRandomGene = getRandomGene;
            this.fitnessFunction = fitnessFunction;

            if (shouldInitGenes)
                for (var i = 0; i < Genes.Length; i++)
                    Genes[i] = getRandomGene();
        }

        public float CalculateFitness(int index)
        {
            Fitness = fitnessFunction(index);
            return Fitness;
        }

        public DNA<T> Crossover(DNA<T> otherParent)
        {
            var child = new DNA<T>(Genes.Length, getRandomGene, fitnessFunction, false);

            for (var i = 0; i < Genes.Length; i++)
                child.Genes[i] = Random.value < 0.5f ? Genes[i] : otherParent.Genes[i];

            return child;
        }

        public void Mutate(float mutationRate)
        {
            for (var i = 0; i < Genes.Length; i++)
                if (Random.value < mutationRate)
                    Genes[i] = getRandomGene();
        }
    }
}