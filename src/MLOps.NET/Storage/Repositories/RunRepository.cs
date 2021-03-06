﻿using MLOps.NET.Entities.Impl;
using MLOps.NET.Storage.Database;
using MLOps.NET.Storage.EntityResolvers;
using MLOps.NET.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MLOps.NET.Storage
{
    ///<inheritdoc cref="IRunRepository"/>
    public sealed class RunRepository : IRunRepository
    {
        private readonly IDbContextFactory contextFactory;
        private readonly IClock clock;
        private readonly IEntityResolver<Run> runResolver;
        private readonly IEntityResolver<RegisteredModel> registeredModelResolver;

        ///<inheritdoc cref="IRunRepository"/>
        public RunRepository(IDbContextFactory contextFactory, IClock clock,
            IEntityResolver<Run> runResolver,
            IEntityResolver<RegisteredModel> registeredModelResolver)
        {
            this.contextFactory = contextFactory;
            this.clock = clock;
            this.runResolver = runResolver;
            this.registeredModelResolver = registeredModelResolver;
        }

        ///<inheritdoc cref="IRunRepository"/>
        public async Task<Run> CreateRunAsync(Guid experimentId, List<PackageDependency> packageDepedencies, string gitCommitHash = "")
        {
            using var db = this.contextFactory.CreateDbContext();
            var run = new Run(experimentId)
            {
                GitCommitHash = gitCommitHash,
                PackageDepedencies = packageDepedencies
            };

            await db.Runs.AddAsync(run);
            await db.SaveChangesAsync();

            return run;
        }

        ///<inheritdoc cref="IRunRepository"/>
        public async Task CreateModelSchemaAsync(Guid runId, List<ModelSchema> modelSchemas)
        {
            using var db = this.contextFactory.CreateDbContext();
            var run = db.Runs.FirstOrDefault(x => x.RunId == runId);

            run.ModelSchemas = modelSchemas;

            await db.SaveChangesAsync();
        }

        ///<inheritdoc cref="IRunRepository"/>
        public Run GetRun(Guid runId)
        {
            using var db = this.contextFactory.CreateDbContext();
            var run = db.Runs.FirstOrDefault(x => x.RunId == runId);

            return this.runResolver.BuildEntity(db, run);
        }

        ///<inheritdoc cref="IRunRepository"/>
        public Run GetRun(string commitHash)
        {
            using var db = this.contextFactory.CreateDbContext();
            var run = db.Runs.FirstOrDefault(x => x.GitCommitHash == commitHash);

            return this.runResolver.BuildEntity(db, run);
        }

        ///<inheritdoc cref="IRunRepository"/>
        public List<Run> GetRuns(Guid experimentId)
        {
            using var db = this.contextFactory.CreateDbContext();

            var runs = db.Runs
                .Where(x => x.ExperimentId == experimentId)
                .ToList();

            return this.runResolver.BuildEntities(db, runs);
        }

        ///<inheritdoc cref="IRunRepository"/>
        public async Task SetTrainingTimeAsync(Guid runId, TimeSpan timeSpan)
        {
            using var db = this.contextFactory.CreateDbContext();
            var existingRun = db.Runs.FirstOrDefault(x => x.RunId == runId);
            if (existingRun == null) throw new InvalidOperationException($"The run with id {runId} does not exist");

            existingRun.TrainingTime = timeSpan;

            await db.SaveChangesAsync();
        }

        ///<inheritdoc cref="IRunRepository"/>
        public async Task<RunArtifact> CreateRunArtifact(Guid runId, string name)
        {
            using var db = this.contextFactory.CreateDbContext();
            var runArtifact = new RunArtifact
            {
                RunId = runId,
                Name = name
            };

            db.RunArtifacts.Add(runArtifact);

            await db.SaveChangesAsync();

            return runArtifact;
        }

        ///<inheritdoc cref="IRunRepository"/>
        public List<RunArtifact> GetRunArtifacts(Guid runId)
        {
            using var db = this.contextFactory.CreateDbContext();

            return db.RunArtifacts
                .Where(x => x.RunId == runId)
                .ToList();
        }

        ///<inheritdoc cref="IRunRepository"/>
        public async Task CreateRegisteredModelAsync(Guid experimentId, Guid runArtifactId, string registeredBy, string modelDescription)
        {
            using var db = this.contextFactory.CreateDbContext();

            var runArtifact = db.RunArtifacts.FirstOrDefault(x => x.RunArtifactId == runArtifactId);
            if (runArtifact == null)
            {
                throw new InvalidOperationException($"The RunArtifact with id {runArtifactId} does not exist. Unable to register a model");
            }

            var run = db.Runs.FirstOrDefault(x => x.RunId == runArtifact.RunId);
            var registeredModels = db.RegisteredModels.Where(x => x.ExperimentId == experimentId);

            var version = registeredModels.Count() > 0 ? registeredModels.Max(x => x.Version) + 1 : 1;

            var registeredModel = new RegisteredModel
            {
                RunArtifactId = runArtifactId,
                RegisteredBy = registeredBy,
                RegisteredDate = this.clock.UtcNow,
                Description = modelDescription,
                Version = version,
                ExperimentId = experimentId,
                RunId = run.RunId
            };

            db.RegisteredModels.Add(registeredModel);

            await db.SaveChangesAsync();
        }

        ///<inheritdoc cref="IRunRepository"/>
        public List<RegisteredModel> GetRegisteredModels(Guid experimentId)
        {
            using var db = this.contextFactory.CreateDbContext();

            var registeredModels = db.RegisteredModels
                .Where(x => x.ExperimentId == experimentId)
                .ToList();

            return this.registeredModelResolver.BuildEntities(db, registeredModels);
        }

        ///<inheritdoc cref="IRunRepository"/>
        public RegisteredModel GetLatestRegisteredModel(Guid experimentId)
        {
            using var db = this.contextFactory.CreateDbContext();

            var latestRegisteredModel = db.RegisteredModels
                .Where(x => x.ExperimentId == experimentId)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();

            return this.registeredModelResolver.BuildEntity(db, latestRegisteredModel);
        }
    }
}
