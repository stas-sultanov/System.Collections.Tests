// Created by Stas Sultanov.
// Copyright © Stas Sultanov.

namespace Tests;

/// <summary>
/// Represents a result of the Collection test.
/// </summary>
public sealed record CollectionTestResult
{
	#region Properties

	/// <summary>
	/// Type of the collection that has been tested.
	/// </summary>
	public Type CollectionType
	{
		get;

		init;
	}

	/// <summary>
	/// The description of the test.
	/// </summary>
	public String Description
	{
		get;

		init;
	}

	/// <summary>
	/// Time taken by the test.
	/// </summary>
	public TimeSpan ElapsedTime
	{
		get;

		init;
	}

	/// <summary>
	/// Count of items in the input collection after executing the test.
	/// </summary>
	public Int32 InputCount
	{
		get;

		init;
	}

	/// <summary>
	/// Count of items in the main collection after executing the test.
	/// </summary>
	public Int32 MainCount
	{
		get;

		init;
	}

	/// <summary>
	/// Count of items in the output collection after executing the test.
	/// </summary>
	public Int32 OutputCount
	{
		get;

		init;
	}

	/// <summary>
	/// Indicates if test has successfully passed.
	/// </summary>
	public Boolean Pass
	{
		get => InputCount == OutputCount && MainCount == 0;
	}

	#endregion
}