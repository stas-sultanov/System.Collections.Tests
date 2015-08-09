﻿namespace System.Collections.Tests
{
	public sealed class CollectionTestResult
	{
		#region Properties

		public String Description
		{
			get;

			set;
		}

		public TimeSpan ElapsedTime
		{
			get;

			set;
		}

		public Int32 InputCount
		{
			get;

			set;
		}

		public Int32 InterimCount
		{
			get;

			set;
		}

		public Int32 OutputCount
		{
			get;

			set;
		}

		public Type CollectionType
		{
			get;

			set;
		}

		public Boolean Pass => InputCount == OutputCount && InterimCount == 0;

		public Int32 WritersCount
		{
			get;

			set;
		}

		#endregion
	}
}