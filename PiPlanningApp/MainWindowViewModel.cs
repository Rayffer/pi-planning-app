﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Newtonsoft.Json;

using PiPlanningApp.Models;
using PiPlanningApp.Repositories;
using PiPlanningApp.Types;

using RestSharp;

namespace PiPlanningApp;

internal partial class MainWindowViewModel : ObservableObject
{
    private readonly IInformationRepository informationRepository;

    [ObservableProperty]
    private double scaleX = 1.0;

    [ObservableProperty]
    private double scaleY = 1.0;

    [ObservableProperty]
    private double offsetX;

    [ObservableProperty]
    private double offsetY;

    [ObservableProperty]
    private List<string> azureIterations;


    private readonly  RestClient client;

    public ObservableCollection<Iteration> Iterations { get; set; } = new();
    public ObservableCollection<Feature> Features { get; set; } = new();
    public ObservableCollection<IterationFeatureSlot> IterationFeatureSlots { get; set; } = new();

    public MainWindowViewModel() : this(new JsonInformationRepository(ConfigurationManager.AppSettings["InformationFile"]))
    {
    }

    internal MainWindowViewModel(IInformationRepository informationRepository)
    {
        this.informationRepository = informationRepository;

        this.informationRepository.ReadInformation();
        this.informationRepository.ApplicationInformation.Features.ForEach(this.AddNewFeature);
        this.informationRepository.ApplicationInformation.Iterations.ForEach(this.AddNewIteration);
        this.IterationFeatureSlots.Clear();
        this.informationRepository.ApplicationInformation.IterationFeatureSlots.ForEach(this.IterationFeatureSlots.Add);

        this.client = new RestClient("https://dev.azure.com/");
    }

    [RelayCommand]
    private void EditAndSaveUserStory(object obj)
    {
        if (obj is not UserStory userStoryToEdit)
        {
            return;
        }

        this.ClearOtherItemsInEdition(obj);

        userStoryToEdit.IsEditing = !userStoryToEdit.IsEditing;
        if (!userStoryToEdit.IsEditing)
        {
            var featureIterationSlot = this.IterationFeatureSlots.First(x => x.UserStories.Contains(userStoryToEdit));
            var parentIteration = this.Iterations.First(x => x.Id == featureIterationSlot.ParentIterationId);
            var parentFeature = this.Features.First(x => x.Id == featureIterationSlot.ParentFeatureId);

            parentIteration.ReservationDays = this.IterationFeatureSlots.Where(x => x.ParentIterationId == parentIteration.Id).Sum(x => x.UserStories.Sum(y => y.Days));
            parentIteration.LoadCapacity = this.IterationFeatureSlots.Where(x => x.ParentIterationId == parentIteration.Id).Sum(x => x.UserStories.Sum(y => y.StoryPoints));
            parentFeature.TotalStoryPoints = this.IterationFeatureSlots.Where(x => x.ParentFeatureId == parentFeature.Id).Sum(x => x.UserStories.Sum(y => y.StoryPoints));

            this.informationRepository.SaveChanges();
        }
    }

    [RelayCommand]
    private void EditAndSaveIteration(object obj)
    {
        if (obj is not Iteration iterationToEdit)
        {
            return;
        }
        this.ClearOtherItemsInEdition(obj);

        iterationToEdit.IsEditing = !iterationToEdit.IsEditing;
        if (!iterationToEdit.IsEditing)
        {
            this.informationRepository.SaveChanges();
        }
    }

    [RelayCommand]
    private void EditAndSaveFeature(object obj)
    {
        if (obj is not Feature featureToEdit)
        {
            return;
        }
        this.ClearOtherItemsInEdition(obj);

        featureToEdit.IsEditing = !featureToEdit.IsEditing;
        if (!featureToEdit.IsEditing)
        {
            this.informationRepository.SaveChanges();
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteRemoveAndSaveIteration))]
    private void RemoveAndSaveIteration(object obj)
    {
        if (obj is not Iteration iteration)
        {
            return;
        }
        this.Iterations.Remove(iteration);
        foreach (var iterationToUpdate in this.Iterations)
        {
            iterationToUpdate.ColumnPosition = this.Iterations.IndexOf(iterationToUpdate);
            iterationToUpdate.IterationNumber = this.Iterations.IndexOf(iterationToUpdate) + 1;
        }
        foreach (var iterationFeatureSlot in this.IterationFeatureSlots.ToList())
        {
            if (iterationFeatureSlot.ParentIterationId == iteration.Id)
            {
                this.IterationFeatureSlots.Remove(iterationFeatureSlot);
                this.informationRepository.ApplicationInformation.IterationFeatureSlots.Remove(iterationFeatureSlot);
                continue;
            }
            iterationFeatureSlot.ColumnPosition = this.Iterations.Single(iteration => iteration.Id == iterationFeatureSlot.ParentIterationId).ColumnPosition;
        }

        this.OnPropertyChanged(nameof(this.Iterations));
        this.OnPropertyChanged(nameof(this.Iterations.Count));

        this.OnPropertyChanged(nameof(this.IterationFeatureSlots));
        this.OnPropertyChanged(nameof(this.IterationFeatureSlots.Count));

        this.informationRepository.ApplicationInformation.Iterations.Remove(iteration);
        this.informationRepository.SaveChanges();
    }

    private bool CanExecuteRemoveAndSaveIteration() => this.Iterations.Count > 1;

    [RelayCommand(CanExecute = nameof(CanExecuteRemoveAndSaveFeature))]
    private void RemoveAndSaveFeature(object obj)
    {
        if (obj is not Feature feature)
        {
            return;
        }
        this.Features.Remove(feature);
        foreach (var featureToUpdate in this.Features)
        {
            featureToUpdate.RowPosition = this.Features.IndexOf(featureToUpdate);
        }
        foreach (var iterationFeatureSlot in this.IterationFeatureSlots.ToList())
        {
            if (iterationFeatureSlot.ParentFeatureId == feature.Id)
            {
                this.IterationFeatureSlots.Remove(iterationFeatureSlot);
                this.informationRepository.ApplicationInformation.IterationFeatureSlots.Remove(iterationFeatureSlot);
                continue;
            }
            iterationFeatureSlot.RowPosition = this.Features.Single(feature => feature.Id == iterationFeatureSlot.ParentFeatureId).RowPosition;
        }

        this.OnPropertyChanged(nameof(this.Features));
        this.OnPropertyChanged(nameof(this.Features.Count));

        this.OnPropertyChanged(nameof(this.IterationFeatureSlots));
        this.OnPropertyChanged(nameof(this.IterationFeatureSlots.Count));

        this.informationRepository.ApplicationInformation.Features.Remove(feature);
        this.informationRepository.SaveChanges();
    }

    private bool CanExecuteRemoveAndSaveFeature() => this.Features.Count > 1;

    [RelayCommand]
    private void RemoveAndSaveUserStory(object obj)
    {
        if (obj is not UserStory userStory)
        {
            return;
        }

        var iterationFeatureSlot = this.IterationFeatureSlots.First(iteration => iteration.UserStories.Contains(userStory));

        var featureIterationSlot = this.IterationFeatureSlots.First(x => x.UserStories.Contains(userStory));
        var parentIteration = this.Iterations.First(x => x.Id == featureIterationSlot.ParentIterationId);
        var parentFeature = this.Features.First(x => x.Id == featureIterationSlot.ParentFeatureId);

        iterationFeatureSlot.RemoveUserStory(userStory);

        parentIteration.ReservationDays = this.IterationFeatureSlots.Where(x => x.ParentIterationId == parentIteration.Id).Sum(x => x.UserStories.Sum(y => y.Days));
        parentIteration.LoadCapacity = this.IterationFeatureSlots.Where(x => x.ParentIterationId == parentIteration.Id).Sum(x => x.UserStories.Sum(y => y.StoryPoints));
        parentFeature.TotalStoryPoints = this.IterationFeatureSlots.Where(x => x.ParentFeatureId == parentFeature.Id).Sum(x => x.UserStories.Sum(y => y.StoryPoints));

        this.informationRepository.SaveChanges();
    }

    [RelayCommand]
    private void SendToAzure()
    {
        this.InsertOrUpdateFeatures();
        this.InsertOrUpdateUserStories();
    }

    private void InsertOrUpdateUserStories()
    {
        throw new NotImplementedException();
    }

    private void InsertOrUpdateIterations()
    {
        throw new NotImplementedException();
    }

    private void InsertOrUpdateFeatures()
    {
        var request = new RestRequest("collection/project/_apis/wit/workitems/$Feature?api-version=7.0", Method.Post);
        request.AddHeader("Content-Type", "application/json-patch+json");
        request.AddHeader("Authorization", "insert token here");
        var body = JsonConvert.SerializeObject(this.Features.Select(feature =>
        {
            return new (string op, string path, string value)[] {
                new ("add", "/fields/System.Title", feature.Title),
                new ("add", "/fields/System.Description", string.Empty),
                new ("add", "/fields/System.Description", string.Empty),
            };
        }));

        request.AddParameter("application/json-patch+json", body, ParameterType.RequestBody);
        var response = client.Execute(request);
        Console.WriteLine(response.Content);
    }

    [RelayCommand]
    private void RestoreZoom()
    {
        this.ScaleX = 1.0;
        this.ScaleY = 1.0;
    }

    [RelayCommand]
    private void ReturnToOrigin()
    {
        this.OffsetX = 0;
        this.OffsetY = 0;
    }

    private void ClearOtherItemsInEdition(object obj)
    {
        foreach (var feature in this.Features)
        {
            if (feature == obj)
            {
                continue;
            }
            feature.IsEditing = false;
        }
        foreach (var iteration in this.Iterations)
        {
            if (iteration == obj)
            {
                continue;
            }
            iteration.IsEditing = false;
        }
        foreach (var iterationFeatureSlot in this.IterationFeatureSlots)
            foreach (var userStory in iterationFeatureSlot.UserStories)
            {
                if (userStory == obj)
                {
                    continue;
                }
                userStory.IsEditing = false;
            }
    }

    [RelayCommand]
    private void AddAndSaveNewFeature()
    {
        var featureToAdd = new Feature
        {
            Id = Guid.NewGuid(),
            RowPosition = this.Features.Count,
            Title = "Feature Title",
            FeatureType = FeatureTypes.Feature
        };

        this.AddNewFeature(featureToAdd);
        this.informationRepository.ApplicationInformation.Features.Add(featureToAdd);
        this.informationRepository.ApplicationInformation.IterationFeatureSlots = this.IterationFeatureSlots.ToList();

        this.informationRepository.SaveChanges();
    }

    [RelayCommand]
    private void AddAndSaveNewIteration()
    {
        var iterationToAdd = new Iteration
        {
            Id = Guid.NewGuid(),
            ColumnPosition = this.Iterations.Count,
            IterationNumber = this.Iterations.Count + 1
        };

        this.AddNewIteration(iterationToAdd);
        this.informationRepository.ApplicationInformation.Iterations.Add(iterationToAdd);
        this.informationRepository.ApplicationInformation.IterationFeatureSlots = this.IterationFeatureSlots.ToList();

        this.informationRepository.SaveChanges();
    }

    [RelayCommand]
    private void AddAndSaveNewUserStory(object obj)
    {
        if (obj is not IterationFeatureSlot iterationFeatureSlot)
        {
            return;
        }

        var userStoryToAdd = new UserStory
        {
            Title = "User story",
            UserStoryTrackingType = UserStoryTrackingTypes.StoryPoints
        };

        iterationFeatureSlot.AddNewUserStory(userStoryToAdd);

        this.informationRepository.SaveChanges();
    }

    private void AddNewFeature(Feature featureToAdd)
    {
        this.Features.Add(featureToAdd);

        foreach (var iteration in this.Iterations)
        {
            this.IterationFeatureSlots.Add(new IterationFeatureSlot
            {
                ColumnPosition = iteration.ColumnPosition,
                ParentIterationId = iteration.Id,
                ParentFeatureId = featureToAdd.Id,
                RowPosition = featureToAdd.RowPosition,
                UserStories = new()
            });
        }

        this.OnPropertyChanged(nameof(this.Features));
        this.OnPropertyChanged(nameof(this.Features.Count));

        this.OnPropertyChanged(nameof(this.IterationFeatureSlots));
        this.OnPropertyChanged(nameof(this.IterationFeatureSlots.Count));
    }

    private void AddNewIteration(Iteration iterationToAdd)
    {
        this.Iterations.Add(iterationToAdd);

        foreach (var feature in this.Features)
        {
            this.IterationFeatureSlots.Add(new IterationFeatureSlot
            {
                ColumnPosition = iterationToAdd.ColumnPosition,
                ParentIterationId = iterationToAdd.Id,
                ParentFeatureId = feature.Id,
                RowPosition = feature.RowPosition,
                UserStories = new()
            });
        }

        this.OnPropertyChanged(nameof(this.Iterations));
        this.OnPropertyChanged(nameof(this.Iterations.Count));

        this.OnPropertyChanged(nameof(this.IterationFeatureSlots));
        this.OnPropertyChanged(nameof(this.IterationFeatureSlots.Count));
    }

    internal void SetScale(double modifiedScaleX, double modifiedScaleY)
    {
        this.ScaleX = modifiedScaleX;
        this.ScaleY = modifiedScaleY;
    }

    internal void SetTranslateOffset(double offsetX, double offsetY)
    {
        this.OffsetX = offsetX;
        this.OffsetY = offsetY;
    }

    public void SaveChanges()
    {
        this.informationRepository.SaveChanges();
    }
}