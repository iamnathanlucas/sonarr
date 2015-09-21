﻿using System;
using System.IO;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.Movies;
using NzbDrone.Core.MediaFiles.Series;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.MediaInfo
{
    [TestFixture]
    public class UpdateMediaInfoServiceFixture : CoreTest<UpdateMediaInfoService>
    {
        private Series _series;
        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            _series = new Series
                      {
                          Id = 1,
                          Path = @"C:\series".AsOsAgnostic()
                      };

            _movie = new Movie
                     {
                         Id = 2,
                         Path = @"C:\Movie".AsOsAgnostic()
                     };

            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.EnableMediaInfo)
                  .Returns(true);
        }

        private void GivenFileExists()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FileExists(It.IsAny<String>()))
                  .Returns(true);
        }

        private void GivenSuccessfulScan()
        {
            Mocker.GetMock<IVideoFileInfoReader>()
                  .Setup(v => v.GetMediaInfo(It.IsAny<String>()))
                  .Returns(new MediaInfoModel());
        }

        private void GivenFailedScan(String path)
        {
            Mocker.GetMock<IVideoFileInfoReader>()
                  .Setup(v => v.GetMediaInfo(path))
                  .Returns((MediaInfoModel)null);
        }

        [Test]
        public void should_get_for_existing_episodefile_on_after_series_scan()
        {
            var episodeFiles = Builder<EpisodeFile>.CreateListOfSize(3)
                .All()
                .With(v => v.RelativePath = "media.mkv")
                .TheFirst(1)
                .With(v => v.MediaInfo = new MediaInfoModel())
                .BuildList();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(v => v.GetFilesBySeries(1))
                  .Returns(episodeFiles);

            GivenFileExists();
            GivenSuccessfulScan();

            Subject.Handle(new SeriesScannedEvent(_series));

            Mocker.GetMock<IVideoFileInfoReader>()
                  .Verify(v => v.GetMediaInfo(Path.Combine(_series.Path, "media.mkv")), Times.Exactly(2));

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Update(It.IsAny<EpisodeFile>()), Times.Exactly(2));
        }

        [Test]
        public void should_get_for_existing_moviefile_on_after_movie_scan()
        {
            var movieFile = Builder<MovieFile>.CreateListOfSize(1)
                .All()
                .With(v => v.RelativePath = "media.mkv")
                .BuildList();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(v => v.GetFileByMovie(2))
                  .Returns(movieFile);

            GivenFileExists();
            GivenSuccessfulScan();

            Subject.Handle(new MovieScannedEvent(_movie));

            Mocker.GetMock<IVideoFileInfoReader>()
                  .Verify(v => v.GetMediaInfo(Path.Combine(_movie.Path, "media.mkv")), Times.Exactly(1));

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Update(It.IsAny<MovieFile>()), Times.Exactly(1));
        }

        [Test]
        public void should_ignore_missing_files()
        {
            var episodeFiles = Builder<EpisodeFile>.CreateListOfSize(2)
                   .All()
                   .With(v => v.RelativePath = "media.mkv")
                   .BuildList();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(v => v.GetFilesBySeries(1))
                  .Returns(episodeFiles);

            GivenSuccessfulScan();

            Subject.Handle(new SeriesScannedEvent(_series));

            Mocker.GetMock<IVideoFileInfoReader>()
                  .Verify(v => v.GetMediaInfo("media.mkv"), Times.Never());

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Update(It.IsAny<EpisodeFile>()), Times.Never());
        }

        [Test]
        public void should_ignore_missing_movie_file()
        {
            var movieFile = Builder<MovieFile>.CreateListOfSize(1)
                   .All()
                   .With(v => v.RelativePath = "media.mkv")
                   .BuildList();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(v => v.GetFileByMovie(2))
                  .Returns(movieFile);

            GivenSuccessfulScan();

            Subject.Handle(new MovieScannedEvent(_movie));

            Mocker.GetMock<IVideoFileInfoReader>()
                  .Verify(v => v.GetMediaInfo("media.mkv"), Times.Never());

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Update(It.IsAny<MovieFile>()), Times.Never());
        }

        [Test]
        public void should_continue_after_failure()
        {
            var episodeFiles = Builder<EpisodeFile>.CreateListOfSize(2)
                   .All()
                   .With(v => v.RelativePath = "media.mkv")
                   .TheFirst(1)
                   .With(v => v.RelativePath = "media2.mkv")
                   .BuildList();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(v => v.GetFilesBySeries(1))
                  .Returns(episodeFiles);

            GivenFileExists();
            GivenSuccessfulScan();
            GivenFailedScan(Path.Combine(_series.Path, "media2.mkv"));

            Subject.Handle(new SeriesScannedEvent(_series));

            Mocker.GetMock<IVideoFileInfoReader>()
                  .Verify(v => v.GetMediaInfo(Path.Combine(_series.Path, "media.mkv")), Times.Exactly(1));

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Update(It.IsAny<EpisodeFile>()), Times.Exactly(1));
        }
    }
}
