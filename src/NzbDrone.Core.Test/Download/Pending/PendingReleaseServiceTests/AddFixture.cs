﻿using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using Marr.Data;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Download.Pending.PendingReleaseServiceTests
{
    [TestFixture]
    public class AddFixture : CoreTest<PendingReleaseService>
    {
        private DownloadDecision _temporarilyRejected;
        private DownloadDecision _movieTemporarilyRejected;
        private Series _series;
        private Movie _movie;
        private Episode _episode;
        private Profile _profile;
        private ReleaseInfo _release;
        private ParsedEpisodeInfo _parsedEpisodeInfo;
        private ParsedMovieInfo _parsedMovieInfo;
        private RemoteEpisode _remoteEpisode;
        private RemoteMovie _remoteMovie;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                                     .Build();

            _movie = Builder<Movie>.CreateNew()
                                   .Build();

            _episode = Builder<Episode>.CreateNew()
                                       .Build();

            _profile = new Profile
                       {
                           Name = "Test",
                           Cutoff = Quality.HDTV720p,
                           Items = new List<ProfileQualityItem>
                                   {
                                       new ProfileQualityItem { Allowed = true, Quality = Quality.HDTV720p },
                                       new ProfileQualityItem { Allowed = true, Quality = Quality.WEBDL720p },
                                       new ProfileQualityItem { Allowed = true, Quality = Quality.Bluray720p }
                                   },
                       };

            _series.Profile = new LazyLoaded<Profile>(_profile);
            _movie.Profile = new LazyLoaded<Profile>(_profile);

            _release = Builder<ReleaseInfo>.CreateNew().Build();

            _parsedEpisodeInfo = Builder<ParsedEpisodeInfo>.CreateNew().Build();
            _parsedEpisodeInfo.Quality = new QualityModel(Quality.HDTV720p);

            _parsedMovieInfo = Builder<ParsedMovieInfo>.CreateNew().Build();
            _parsedEpisodeInfo.Quality = new QualityModel(Quality.HDTV720p);

            _remoteEpisode = new RemoteEpisode();
            _remoteEpisode.Episodes = new List<Episode> { _episode };
            _remoteEpisode.Series = _series;
            _remoteEpisode.ParsedEpisodeInfo = _parsedEpisodeInfo;
            _remoteEpisode.Release = _release;

            _remoteMovie = new RemoteMovie();
            _remoteMovie.Movie = _movie;
            _remoteMovie.ParsedMovieInfo = _parsedMovieInfo;
            _remoteMovie.Release = _release;

            _temporarilyRejected = new DownloadDecision(_remoteEpisode, new Rejection("Temp Rejected", RejectionType.Temporary));
            _movieTemporarilyRejected = new DownloadDecision(_remoteMovie, new Rejection("Temp Rejected", RejectionType.Temporary));

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<PendingRelease>());

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(It.IsAny<Int32>()))
                  .Returns(_series);

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetMovie(It.IsAny<Int32>()))
                  .Returns(_movie);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetEpisodes(It.IsAny<ParsedEpisodeInfo>(), _series, true, null))
                  .Returns(new List<Episode> { _episode });


            Mocker.GetMock<IPrioritizeDownloadDecision>()
                  .Setup(s => s.PrioritizeDecisions(It.IsAny<List<DownloadDecision>>()))
                  .Returns((List<DownloadDecision> d) => d);
        }

        private void GivenHeldRelease(String title, String indexer, DateTime publishDate)
        {
            var release = _release.JsonClone();
            release.Indexer = indexer;
            release.PublishDate = publishDate;


            var heldReleases = Builder<PendingRelease>.CreateListOfSize(1)
                                                   .All()
                                                   .With(h => h.MovieId = 0)
                                                   .With(h => h.SeriesId = _series.Id)
                                                   .With(h => h.Title = title)
                                                   .With(h => h.Release = release)
                                                   .Build();

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(s => s.All())
                  .Returns(heldReleases);
        }

        private void GivenMovieHeldRelease(String title, String indexer, DateTime publishDate)
        {
            var release = _release.JsonClone();
            release.Indexer = indexer;
            release.PublishDate = publishDate;


            var heldReleases = Builder<PendingRelease>.CreateListOfSize(1)
                                                   .All()
                                                   .With(h => h.SeriesId = 0)
                                                   .With(h => h.MovieId = _movie.Id)
                                                   .With(h => h.Title = title)
                                                   .With(h => h.Release = release)
                                                   .Build();

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(s => s.All())
                  .Returns(heldReleases);
        }

        [Test]
        public void should_add()
        {
            Subject.Add(_temporarilyRejected);

            VerifyInsert();
        }

        [Test]
        public void should_add_movie()
        {
            Subject.Add(_movieTemporarilyRejected);

            VerifyInsert();
        }

        [Test]
        public void should_not_add_if_it_is_the_same_release_from_the_same_indexer()
        {
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate);

            Subject.Add(_temporarilyRejected);

            VerifyNoInsert();
        }

        [Test]
        public void should_not_add_movie_if_it_is_the_same_release_from_the_same_indexer()
        {
            GivenMovieHeldRelease(_release.Title, _release.Indexer, _release.PublishDate);

            Subject.Add(_movieTemporarilyRejected);

            VerifyNoInsert();
        }

        [Test]
        public void should_add_if_title_is_different()
        {
            GivenHeldRelease(_release.Title + "-RP", _release.Indexer, _release.PublishDate);

            Subject.Add(_temporarilyRejected);

            VerifyInsert();
        }

        [Test]
        public void should_add_if_movie_title_is_different()
        {
            GivenMovieHeldRelease(_release.Title + "-RP", _release.Indexer, _release.PublishDate);

            Subject.Add(_movieTemporarilyRejected);

            VerifyInsert();
        }

        [Test]
        public void should_add_if_indexer_is_different()
        {
            GivenHeldRelease(_release.Title, "AnotherIndexer", _release.PublishDate);

            Subject.Add(_temporarilyRejected);

            VerifyInsert();
        }

        [Test]
        public void should_add_if_movie_indexer_is_different()
        {
            GivenMovieHeldRelease(_release.Title, "AnotherIndexer", _release.PublishDate);

            Subject.Add(_movieTemporarilyRejected);

            VerifyInsert();
        }

        [Test]
        public void should_add_if_publish_date_is_different()
        {
            GivenHeldRelease(_release.Title, _release.Indexer, _release.PublishDate.AddHours(1));

            Subject.Add(_temporarilyRejected);

            VerifyInsert();
        }

        [Test]
        public void should_add_if_movie_publish_date_is_different()
        {
            GivenMovieHeldRelease(_release.Title, _release.Indexer, _release.PublishDate.AddHours(1));

            Subject.Add(_movieTemporarilyRejected);

            VerifyInsert();
        }

        private void VerifyInsert()
        {
            Mocker.GetMock<IPendingReleaseRepository>()
                .Verify(v => v.Insert(It.IsAny<PendingRelease>()), Times.Once());
        }

        private void VerifyNoInsert()
        {
            Mocker.GetMock<IPendingReleaseRepository>()
                .Verify(v => v.Insert(It.IsAny<PendingRelease>()), Times.Never());
        }
    }
}
