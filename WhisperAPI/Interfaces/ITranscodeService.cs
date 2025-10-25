// <copyright file="ITranscodeService.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// https://github.com/drasticactions/MauiWhisper
// </copyright>

namespace WhisperAPI.Interfaces;

/// <summary>
/// Transcode Service.
/// </summary>
public interface ITranscodeService
{
    string BasePath { get; }

    /// <summary>
    /// Process file.
    /// </summary>
    /// <param name="filePath">File Path.</param>
    /// <returns>Path to transcoded file.</returns>
    Task<string> ProcessFile(string filePath);

    /// <summary>
    /// Gets the duration of an audio file in seconds.
    /// </summary>
    /// <param name="filePath">File Path.</param>
    /// <returns>Duration in seconds, or null if unable to determine.</returns>
    Task<double?> GetDurationAsync(string filePath);
}