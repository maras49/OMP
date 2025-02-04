﻿using OpticalMappingParser.Core.Interfaces;
using OpticalMappingParser.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OpticalMappingParser.Core.Implementation
{
    public class DifficultAreaIdentifier : IDifficultAreaIdentifier
    {
        // (chromosomeId, [position])
        private Dictionary<int, List<int>> _chromosomes;

        public DifficultAreaIdentifier()
        {
        }

        public DifficultAreaIdentifier(string path)
        {
            LoadFile(path);
        }

        public IEnumerable<int> Chromosomes
        {
            get
            {
                CheckDataInitialized();
                return _chromosomes.Keys;
            }
        }

        public void LoadFile(string path)
        {
            LoadFileFromMemory(File.ReadAllLines(path));
        }

        public void LoadFileFromMemory(string[] data)
        {
            _chromosomes = data
                .Where(e => e[0] != '#')
                .Select(e => e.Split('\t'))
                .Select(e => new
                {
                    Chromosome = int.Parse(e[0]),
                    Position = (int)float.Parse(e[5], CultureInfo.InvariantCulture),
                })
                .GroupBy(e => e.Chromosome)
                .ToDictionary(e => e.Key, e => e.Select(x => x.Position).ToList());
        }

        public void SaveToCsv(string path, IEnumerable<DifficultAreaResult> results)
        {
            const string header = "chromosome,start,end,area_type";

            var sb = new StringBuilder();
            sb.AppendLine(header);
            foreach (var result in results)
                sb.AppendLine(ToCsvRow(result));

            File.WriteAllText(path, sb.ToString());
        }

        public IList<DifficultAreaResult> Process(int minLongDistance, int maxShortDistance, int minShortDistanceSequentMarksCount)
        {
            CheckDataInitialized();

            var result = new List<DifficultAreaResult>();
            foreach (var chromosome in _chromosomes)
            {
                result.AddRange(ProcessChromosome(chromosome.Value, chromosome.Key, minLongDistance, maxShortDistance, minShortDistanceSequentMarksCount));
            }

            return result;
        }

        public IList<DifficultAreaResult> Process(int minLongDistance, int maxShortDistance, int minShortDistanceSequentMarksCount, int chromosomeId, int? fromPosition = null, int? toPosition = null)
        {
            CheckDataInitialized();

            if (!_chromosomes.ContainsKey(chromosomeId))
                throw new ArgumentOutOfRangeException($"Chromosome {chromosomeId} does not exist");

            return ProcessChromosome(_chromosomes[chromosomeId], chromosomeId, minLongDistance, maxShortDistance, minShortDistanceSequentMarksCount, fromPosition, toPosition);
        }

        private IList<DifficultAreaResult> ProcessChromosome(List<int> chromosome, int chromosomeId, int minLongDistance, int maxShortDistance, int minShortDistanceSequentMarksCount, int? fromPosition = null, int? toPosition = null)
        {
            var positions = chromosome.ToList();
            if (fromPosition.HasValue)
                positions = positions.Where(e => e >= fromPosition).ToList();
            if (toPosition.HasValue)
                positions = positions.Where(e => e <= toPosition).ToList();

            var result = new List<DifficultAreaResult>();
            int shortAreaStart = -1;
            int longAreaStart = -1;

            void AddResult(int currentPosition, DifficultAreaType sequenceLength)
            {
                result.Add(new DifficultAreaResult
                {
                    Chromosome = chromosomeId,
                    StartPosition = positions[sequenceLength == DifficultAreaType.Short ? shortAreaStart : longAreaStart],
                    EndPosition = positions[currentPosition - 1],
                    DifficultAreaType = sequenceLength,
                });

                if (sequenceLength == DifficultAreaType.Short)
                    shortAreaStart = -1;
                else
                    longAreaStart = -1;
            }

            for (int i = 1; i < positions.Count; i++)
            {
                int distance = positions[i] - positions[i - 1];

                // handle short difficult area
                if (distance <= maxShortDistance)
                {
                    if (shortAreaStart == -1) // start new area
                        shortAreaStart = i - 1;
                }
                else
                {
                    if (shortAreaStart != -1) // end area
                    {
                        if ((i - 1) - shortAreaStart >= minShortDistanceSequentMarksCount)
                            AddResult(i, DifficultAreaType.Short);

                        shortAreaStart = -1;
                    }
                }

                // handle long difficult area
                if (distance > minLongDistance)
                {
                    if (longAreaStart == -1) // start new area
                        longAreaStart = i - 1;
                }
                else
                {
                    if (longAreaStart != -1) // end area
                        AddResult(i, DifficultAreaType.Long);
                }
            }

            // handle unresolved difficult areas
            if (shortAreaStart != -1 && positions.Count - 1 - shortAreaStart >= minShortDistanceSequentMarksCount)
                AddResult(positions.Count, DifficultAreaType.Short);
            if (longAreaStart != -1)
                AddResult(positions.Count, DifficultAreaType.Long);

            return result;
        }

        private void CheckDataInitialized()
        {
            if (_chromosomes == null)
                throw new InvalidOperationException("Data not initialized");
        }

        private string ToCsvRow(DifficultAreaResult result)
        {
            return $"{result.Chromosome},{result.StartPosition},{result.EndPosition},{(result.DifficultAreaType == DifficultAreaType.Short ? "S" : "L")}";
        }
    }
}
