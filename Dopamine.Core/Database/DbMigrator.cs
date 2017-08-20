﻿using Dopamine.Core.Base;
using Dopamine.Core.Logging;
using System;
using System.Reflection;

namespace Dopamine.Core.Database
{
    public abstract class DbMigrator : IDbMigrator
    {
        #region DatabaseVersionAttribute
        protected sealed class DatabaseVersionAttribute : Attribute
        {
            private int version;

            public DatabaseVersionAttribute(int version)
            {
                this.version = version;
            }

            public int Version
            {
                get { return this.version; }
            }
        }
        #endregion

        #region Variables
        // NOTE: whenever there is a change in the database schema,
        // this version MUST be incremented and a migration method
        // MUST be supplied to match the new version number
        protected const int CURRENT_VERSION = 19;
        private ISQLiteConnectionFactory factory;
        private int userDatabaseVersion;
        #endregion

        #region Construction
        public DbMigrator(ISQLiteConnectionFactory factory)
        {
            this.factory = factory;
        }
        #endregion

        #region Properties
        public ISQLiteConnectionFactory Factory => this.factory;
        #endregion

        #region Fresh database setup
        private void CreateConfiguration()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("CREATE TABLE Configuration (" +
                             "ConfigurationID    INTEGER," +
                             "Key                TEXT," +
                             "Value              TEXT," +
                             "PRIMARY KEY(ConfigurationID));");

                conn.Execute(String.Format("INSERT INTO Configuration (ConfigurationID, Key, Value) VALUES (null,'DatabaseVersion', {0});", CURRENT_VERSION));
            }
        }

        private void CreateTablesAndIndexes()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("CREATE TABLE Artist (" +
                             "ArtistID           INTEGER," +
                             "ArtistName	     TEXT," +
                             "PRIMARY KEY(ArtistID));");

                conn.Execute("CREATE INDEX ArtistIndex ON Artist(ArtistName);");

                conn.Execute("CREATE TABLE Genre (" +
                             "GenreID           INTEGER," +
                             "GenreName	        TEXT," +
                             "PRIMARY KEY(GenreID));");

                conn.Execute("CREATE INDEX GenreIndex ON Genre(GenreName);");

                conn.Execute("CREATE TABLE Album (" +
                             "AlbumID	        INTEGER," +
                             "AlbumTitle	    TEXT," +
                             "AlbumArtist	    TEXT," +
                             "Year	            INTEGER," +
                             "ArtworkID	        TEXT," +
                             "DateLastSynced	INTEGER," +
                             "DateAdded	        INTEGER," +
                             "DateCreated	    INTEGER," +
                             "PRIMARY KEY(AlbumID));");

                conn.Execute("CREATE INDEX AlbumIndex ON Album(AlbumTitle, AlbumArtist);");
                conn.Execute("CREATE INDEX AlbumYearIndex ON Album(Year);");

                conn.Execute("CREATE TABLE Folder (" +
                             "FolderID	         INTEGER PRIMARY KEY AUTOINCREMENT," +
                             "Path	             TEXT," +
                             "SafePath	             TEXT," +
                             "ShowInCollection   INTEGER);");

                conn.Execute("CREATE TABLE Track (" +
                             "TrackID	            INTEGER," +
                             "ArtistID	            INTEGER," +
                             "GenreID	            INTEGER," +
                             "AlbumID	            INTEGER," +
                             "FolderID	            INTEGER," +
                             "Path	                TEXT," +
                             "SafePath	            TEXT," +
                             "FileName	            TEXT," +
                             "MimeType	            TEXT," +
                             "FileSize	            INTEGER," +
                             "BitRate	            INTEGER," +
                             "SampleRate	        INTEGER," +
                             "TrackTitle	        TEXT," +
                             "TrackNumber	        INTEGER," +
                             "TrackCount	        INTEGER," +
                             "DiscNumber	        INTEGER," +
                             "DiscCount	            INTEGER," +
                             "Duration	            INTEGER," +
                             "Year	                INTEGER," +
                             "HasLyrics	            INTEGER," +
                             "DateAdded  	        INTEGER," +
                             "DateLastSynced	    INTEGER," +
                             "DateFileModified	    INTEGER," +
                             "MetaDataHash	        TEXT," +
                             "NeedsIndexing 	    INTEGER," +
                             "PRIMARY KEY(TrackID));");

                conn.Execute("CREATE INDEX TrackArtistIDIndex ON Track(ArtistID);");
                conn.Execute("CREATE INDEX TrackAlbumIDIndex ON Track(AlbumID);");
                conn.Execute("CREATE INDEX TrackGenreIDIndex ON Track(GenreID);");
                conn.Execute("CREATE INDEX TrackFolderIDIndex ON Track(FolderID);");
                conn.Execute("CREATE INDEX TrackPathIndex ON Track(Path);");
                conn.Execute("CREATE INDEX TrackSafePathIndex ON Track(SafePath);");

                conn.Execute("CREATE TABLE RemovedTrack (" +
                             "TrackID	            INTEGER," +
                             "Path	                TEXT," +
                             "SafePath	            TEXT," +
                             "DateRemoved           INTEGER," +
                             "PRIMARY KEY(TrackID));");

                conn.Execute("CREATE TABLE QueuedTrack (" +
                             "QueuedTrackID         INTEGER," +
                             "QueueID	            TEXT," +
                             "Path	                TEXT," +
                             "SafePath	            TEXT," +
                             "IsPlaying             INTEGER," +
                             "ProgressSeconds       INTEGER," +
                             "OrderID               INTEGER," +
                             "PRIMARY KEY(QueuedTrackID));");

                conn.Execute("CREATE TABLE IndexingStatistic (" +
                            "IndexingStatisticID    INTEGER," +
                            "Key                    TEXT," +
                            "Value                  TEXT," +
                            "PRIMARY KEY(IndexingStatisticID));");

                conn.Execute("CREATE TABLE TrackStatistic (" +
                             "TrackStatisticID	    INTEGER PRIMARY KEY AUTOINCREMENT," +
                             "Path	                TEXT," +
                             "SafePath	            TEXT," +
                             "Rating	            INTEGER," +
                             "Love	                INTEGER," +
                             "PlayCount	            INTEGER," +
                             "SkipCount	            INTEGER," +
                             "DateLastPlayed        INTEGER);");

                conn.Execute("CREATE INDEX TrackStatisticSafePathIndex ON Track(SafePath);");
            }
        }
        #endregion

        #region Version 1
        [DatabaseVersion(1)]
        private void Migrate1()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("ALTER TABLE Tracks ADD DiscNumber INTEGER;");
                conn.Execute("ALTER TABLE Tracks ADD DiscCount INTEGER;");

                conn.Execute("UPDATE Tracks SET DiscNumber=(SELECT DiscNumber FROM Albums WHERE Albums.AlbumID=Tracks.AlbumID);");
                conn.Execute("UPDATE Tracks SET DiscCount=(SELECT DiscCount FROM Albums WHERE Albums.AlbumID=Tracks.AlbumID);");

                conn.Execute("CREATE TABLE Albums_Backup (" +
                             "AlbumID	            INTEGER," +
                             "AlbumTitle	        TEXT," +
                             "AlbumArtist	        TEXT," +
                             "EmbeddedArtworkID	    TEXT," +
                             "EmbeddedArtworkSize   INTEGER," +
                             "ExternalArtworkID	    TEXT," +
                             "ExternalArtworkSize   INTEGER," +
                             "ExternalArtworkPath	TEXT," +
                             "ExternalArtworkDateFileModified	INTEGER," +
                             "First_AlbumID INTEGER," +
                             "PRIMARY KEY(AlbumID));");

                conn.Execute("INSERT INTO Albums_Backup SELECT AlbumID," +
                             "AlbumTitle," +
                             "AlbumArtist," +
                             "EmbeddedArtworkID," +
                             "EmbeddedArtworkSize," +
                             "ExternalArtworkID," +
                             "ExternalArtworkSize," +
                             "ExternalArtworkPath," +
                             "ExternalArtworkDateFileModified, (SELECT AlbumID FROM Albums ab WHERE LOWER(TRIM(a.AlbumTitle))=LOWER(TRIM(ab.AlbumTitle)) AND LOWER(TRIM(a.AlbumArtist))=LOWER(TRIM(ab.AlbumArtist)) ORDER BY AlbumID LIMIT 1) " +
                             "FROM Albums a;");

                conn.Execute("UPDATE Tracks SET AlbumID=(SELECT First_AlbumID FROM Albums_Backup WHERE Albums_Backup.AlbumID=Tracks.AlbumID);");
                conn.Execute("DROP TABLE Albums;");

                conn.Execute("CREATE TABLE Albums (" +
                             "AlbumID	            INTEGER," +
                             "AlbumTitle	        TEXT," +
                             "AlbumArtist	        TEXT," +
                             "EmbeddedArtworkID	    TEXT," +
                             "EmbeddedArtworkSize   INTEGER," +
                             "ExternalArtworkID	    TEXT," +
                             "ExternalArtworkSize   INTEGER," +
                             "ExternalArtworkPath	TEXT," +
                             "ExternalArtworkDateFileModified	INTEGER," +
                             "PRIMARY KEY(AlbumID));");

                conn.Execute("INSERT INTO Albums SELECT AlbumID," +
                             "AlbumTitle," +
                             "AlbumArtist," +
                             "EmbeddedArtworkID," +
                             "EmbeddedArtworkSize," +
                             "ExternalArtworkID," +
                             "ExternalArtworkSize," +
                             "ExternalArtworkPath," +
                             "ExternalArtworkDateFileModified " +
                             "FROM Albums_Backup WHERE AlbumID=First_AlbumID;");

                conn.Execute("DROP TABLE Albums_Backup;");

                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 2
        [DatabaseVersion(2)]
        private void Migrate2()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("CREATE TABLE Albums_Backup (" +
                             "AlbumID	        INTEGER," +
                             "AlbumTitle	    TEXT," +
                             "AlbumArtist	    TEXT," +
                             "PRIMARY KEY(AlbumID));");

                conn.Execute("INSERT INTO Albums_Backup SELECT AlbumID," +
                             "AlbumTitle," +
                             "AlbumArtist " +
                             "FROM Albums;");

                conn.Execute("DROP TABLE Albums;");

                conn.Execute("CREATE TABLE Albums (" +
                             "AlbumID	        INTEGER," +
                             "AlbumTitle	    TEXT," +
                             "AlbumArtist	    TEXT," +
                             "Year	            INTEGER," +
                             "ArtworkID	        TEXT," +
                             "DateLastSynced	INTEGER," +
                             "PRIMARY KEY(AlbumID));");

                conn.Execute("INSERT INTO Albums SELECT AlbumID," +
                             "AlbumTitle," +
                             "AlbumArtist," +
                             "0," +
                             "null," +
                             "0 " +
                             "FROM Albums_Backup;");

                conn.Execute("DROP TABLE Albums_Backup;");

                conn.Execute("CREATE INDEX IF NOT EXISTS TracksFolderIDIndex ON Tracks(FolderID);");
                conn.Execute("CREATE INDEX IF NOT EXISTS TracksArtistIDIndex ON Tracks(ArtistID);");
                conn.Execute("CREATE INDEX IF NOT EXISTS TracksAlbumIDIndex ON Tracks(AlbumID);");
                conn.Execute("CREATE INDEX IF NOT EXISTS TracksPathIndex ON Tracks(Path);");
                conn.Execute("CREATE INDEX IF NOT EXISTS ArtistsIndex ON Artists(ArtistName);");
                conn.Execute("CREATE INDEX IF NOT EXISTS AlbumsIndex ON Albums(AlbumTitle, AlbumArtist);");

                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 3
        [DatabaseVersion(3)]
        private void Migrate3()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("CREATE TABLE RemovedTracks (" +
                             "TrackID	            INTEGER," +
                             "Path	                TEXT," +
                             "DateRemoved           INTEGER," +
                             "PRIMARY KEY(TrackID));");

                conn.Execute("BEGIN TRANSACTION;");
                conn.Execute("CREATE TEMPORARY TABLE Tracks_Backup (" +
                                     "TrackID	            INTEGER," +
                                     "ArtistID	            INTEGER," +
                                     "AlbumID	            INTEGER," +
                                     "Path	                TEXT," +
                                     "FileName	            TEXT," +
                                     "MimeType	            TEXT," +
                                     "FileSize	            INTEGER," +
                                     "BitRate	            INTEGER," +
                                     "SampleRate	        INTEGER," +
                                     "TrackTitle	        TEXT," +
                                     "TrackNumber	        INTEGER," +
                                     "TrackCount	        INTEGER," +
                                     "DiscNumber	        INTEGER," +
                                     "DiscCount	            INTEGER," +
                                     "Duration	            INTEGER," +
                                     "Year	                INTEGER," +
                                     "Genre	                TEXT," +
                                     "Rating	            INTEGER," +
                                     "PlayCount	            INTEGER," +
                                     "SkipCount	            INTEGER," +
                                     "DateAdded  	        INTEGER," +
                                     "DateLastPlayed        INTEGER," +
                                     "DateLastSynced	    INTEGER," +
                                     "DateFileModified	    INTEGER," +
                                     "MetaDataHash	        TEXT," +
                                     "PRIMARY KEY(TrackID));");

                conn.Execute("INSERT INTO Tracks_Backup SELECT TrackID," +
                                     "ArtistID," +
                                     "AlbumID," +
                                     "Path," +
                                     "FileName," +
                                     "MimeType," +
                                     "FileSize," +
                                     "BitRate," +
                                     "SampleRate," +
                                     "TrackTitle," +
                                     "TrackNumber," +
                                     "TrackCount," +
                                     "DiscNumber," +
                                     "DiscCount," +
                                     "Duration," +
                                     "Year," +
                                     "Genre," +
                                     "Rating," +
                                     "PlayCount," +
                                     "SkipCount," +
                                     "DateAdded," +
                                     "DateLastPlayed," +
                                     "DateLastSynced," +
                                     "DateFileModified," +
                                     "MetaDataHash " +
                                     "FROM Tracks;");

                conn.Execute("DROP TABLE Tracks;");

                conn.Execute("CREATE TABLE Tracks (" +
                             "TrackID	            INTEGER," +
                             "ArtistID	            INTEGER," +
                             "AlbumID	            INTEGER," +
                             "Path	                TEXT," +
                             "FileName	            TEXT," +
                             "MimeType	            TEXT," +
                             "FileSize	            INTEGER," +
                             "BitRate	            INTEGER," +
                             "SampleRate	        INTEGER," +
                             "TrackTitle	        TEXT," +
                             "TrackNumber	        INTEGER," +
                             "TrackCount	        INTEGER," +
                             "DiscNumber	        INTEGER," +
                             "DiscCount	            INTEGER," +
                             "Duration	            INTEGER," +
                             "Year	                INTEGER," +
                             "Genre	                TEXT," +
                             "Rating	            INTEGER," +
                             "PlayCount	            INTEGER," +
                             "SkipCount	            INTEGER," +
                             "DateAdded  	        INTEGER," +
                             "DateLastPlayed        INTEGER," +
                             "DateLastSynced	    INTEGER," +
                             "DateFileModified	    INTEGER," +
                             "MetaDataHash	        TEXT," +
                             "PRIMARY KEY(TrackID));");

                conn.Execute("INSERT INTO Tracks SELECT TrackID," +
                                    "ArtistID," +
                                    "AlbumID," +
                                    "Path," +
                                    "FileName," +
                                    "MimeType," +
                                    "FileSize," +
                                    "BitRate," +
                                    "SampleRate," +
                                    "TrackTitle," +
                                    "TrackNumber," +
                                    "TrackCount," +
                                    "DiscNumber," +
                                    "DiscCount," +
                                    "Duration," +
                                    "Year," +
                                    "Genre," +
                                    "Rating," +
                                    "PlayCount," +
                                    "SkipCount," +
                                    "DateAdded," +
                                    "DateLastPlayed," +
                                    "DateLastSynced," +
                                    "DateFileModified," +
                                    "MetaDataHash " +
                                    "FROM Tracks_Backup;");

                conn.Execute("DROP TABLE Tracks_Backup;");

                conn.Execute("COMMIT;");

                conn.Execute("CREATE INDEX IF NOT EXISTS TracksArtistIDIndex ON Tracks(ArtistID);");
                conn.Execute("CREATE INDEX IF NOT EXISTS TracksAlbumIDIndex ON Tracks(AlbumID);");
                conn.Execute("CREATE INDEX TracksPathIndex ON Tracks(Path)");

                conn.Execute("ALTER TABLE Albums ADD DateAdded INTEGER;");
                conn.Execute("UPDATE Albums SET DateAdded=(SELECT MIN(DateAdded) FROM Tracks WHERE Tracks.AlbumID = Albums.AlbumID);");

                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 4
        [DatabaseVersion(4)]
        private void Migrate4()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("CREATE TABLE Genres (" +
                             "GenreID           INTEGER," +
                             "GenreName	        TEXT," +
                             "PRIMARY KEY(GenreID));");

                conn.Execute("ALTER TABLE Tracks ADD GenreID INTEGER;");

                conn.Execute("INSERT INTO Genres(GenreName) SELECT DISTINCT Genre FROM Tracks WHERE TRIM(Genre) <>'';");
                conn.Execute("UPDATE Tracks SET GenreID=(SELECT GenreID FROM Genres WHERE Genres.GenreName=Tracks.Genre) WHERE TRIM(Genre) <> '';");

                conn.Execute(String.Format("INSERT INTO Genres(GenreName) VALUES('{0}');", Defaults.UnknownGenreString));
                conn.Execute(String.Format("UPDATE Tracks SET GenreID=(SELECT GenreID FROM Genres WHERE Genres.GenreName='{0}') WHERE TRIM(Genre) = '';", Defaults.UnknownGenreString));

                conn.Execute("CREATE TABLE Tracks_Backup (" +
                             "TrackID	            INTEGER," +
                             "ArtistID	            INTEGER," +
                             "GenreID	            INTEGER," +
                             "AlbumID	            INTEGER," +
                             "Path	                TEXT," +
                             "FileName	            TEXT," +
                             "MimeType	            TEXT," +
                             "FileSize	            INTEGER," +
                             "BitRate	            INTEGER," +
                             "SampleRate	        INTEGER," +
                             "TrackTitle	        TEXT," +
                             "TrackNumber	        INTEGER," +
                             "TrackCount	        INTEGER," +
                             "DiscNumber	        INTEGER," +
                             "DiscCount	            INTEGER," +
                             "Duration	            INTEGER," +
                             "Year	                INTEGER," +
                             "Rating	            INTEGER," +
                             "PlayCount	            INTEGER," +
                             "SkipCount	            INTEGER," +
                             "DateAdded  	        INTEGER," +
                             "DateLastPlayed        INTEGER," +
                             "DateLastSynced	    INTEGER," +
                             "DateFileModified	    INTEGER," +
                             "MetaDataHash	        TEXT," +
                             "PRIMARY KEY(TrackID));");

                conn.Execute("INSERT INTO Tracks_Backup SELECT TrackID," +
                                     "ArtistID," +
                                     "GenreID," +
                                     "AlbumID," +
                                     "Path," +
                                     "FileName," +
                                     "MimeType," +
                                     "FileSize," +
                                     "BitRate," +
                                     "SampleRate," +
                                     "TrackTitle," +
                                     "TrackNumber," +
                                     "TrackCount," +
                                     "DiscNumber," +
                                     "DiscCount," +
                                     "Duration," +
                                     "Year," +
                                     "Rating," +
                                     "PlayCount," +
                                     "SkipCount," +
                                     "DateAdded," +
                                     "DateLastPlayed," +
                                     "DateLastSynced," +
                                     "DateFileModified," +
                                     "MetaDataHash " +
                                     "FROM Tracks;");

                conn.Execute("DROP TABLE Tracks;");

                conn.Execute("CREATE TABLE Tracks (" +
                             "TrackID	            INTEGER," +
                             "ArtistID	            INTEGER," +
                             "GenreID	            INTEGER," +
                             "AlbumID	            INTEGER," +
                             "Path	                TEXT," +
                             "FileName	            TEXT," +
                             "MimeType	            TEXT," +
                             "FileSize	            INTEGER," +
                             "BitRate	            INTEGER," +
                             "SampleRate	        INTEGER," +
                             "TrackTitle	        TEXT," +
                             "TrackNumber	        INTEGER," +
                             "TrackCount	        INTEGER," +
                             "DiscNumber	        INTEGER," +
                             "DiscCount	            INTEGER," +
                             "Duration	            INTEGER," +
                             "Year	                INTEGER," +
                             "Rating	            INTEGER," +
                             "PlayCount	            INTEGER," +
                             "SkipCount	            INTEGER," +
                             "DateAdded  	        INTEGER," +
                             "DateLastPlayed        INTEGER," +
                             "DateLastSynced	    INTEGER," +
                             "DateFileModified	    INTEGER," +
                             "MetaDataHash	        TEXT," +
                             "PRIMARY KEY(TrackID));");

                conn.Execute("INSERT INTO Tracks SELECT TrackID," +
                                   "ArtistID," +
                                   "GenreID," +
                                   "AlbumID," +
                                   "Path," +
                                   "FileName," +
                                   "MimeType," +
                                   "FileSize," +
                                   "BitRate," +
                                   "SampleRate," +
                                   "TrackTitle," +
                                   "TrackNumber," +
                                   "TrackCount," +
                                   "DiscNumber," +
                                   "DiscCount," +
                                   "Duration," +
                                   "Year," +
                                   "Rating," +
                                   "PlayCount," +
                                   "SkipCount," +
                                   "DateAdded," +
                                   "DateLastPlayed," +
                                   "DateLastSynced," +
                                   "DateFileModified," +
                                   "MetaDataHash " +
                                   "FROM Tracks_Backup;");

                conn.Execute("DROP TABLE Tracks_Backup;");

                conn.Execute("CREATE INDEX IF NOT EXISTS TracksGenreIDIndex ON Tracks(GenreID);");
                conn.Execute("CREATE INDEX GenresIndex ON Genres(GenreName);");
            }
        }
        #endregion

        #region Version 5
        [DatabaseVersion(5)]
        private void Migrate5()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("UPDATE Albums SET Year=(SELECT MAX(Year) FROM Tracks WHERE Tracks.AlbumID=Albums.AlbumID) WHERE AlbumTitle<>'Unknown Album';");
                conn.Execute("CREATE INDEX IF NOT EXISTS AlbumsYearIndex ON Albums(Year);");
            }
        }
        #endregion

        #region Version 6
        [DatabaseVersion(6)]
        private void Migrate6()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("ALTER TABLE Tracks ADD FolderID INTEGER;");
                conn.Execute("UPDATE Tracks SET FolderID=(SELECT FolderID FROM Folders WHERE UPPER(Tracks.Path) LIKE UPPER(Folders.Path)||'%');");

                conn.Execute("CREATE INDEX IF NOT EXISTS TracksFolderIDIndex ON Tracks(FolderID);");
            }
        }
        #endregion

        #region Version 7
        [DatabaseVersion(7)]
        private void Migrate7()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("ALTER TABLE Folders ADD ShowInCollection INTEGER;");
                conn.Execute("UPDATE Folders SET ShowInCollection=1;");
            }
        }
        #endregion

        #region Version 8
        [DatabaseVersion(8)]
        private void Migrate8()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("CREATE TABLE QueuedTracks (" +
                             "QueuedTrackID     INTEGER," +
                             "Path	             TEXT," +
                             "OrderID           INTEGER," +
                             "PRIMARY KEY(QueuedTrackID));");
            }
        }
        #endregion

        #region Version 9
        [DatabaseVersion(9)]
        private void Migrate9()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("CREATE TABLE IndexingStatistics (" +
                             "IndexingStatisticID    INTEGER," +
                             "Key                    TEXT," +
                             "Value                  TEXT," +
                             "PRIMARY KEY(IndexingStatisticID));");
            }
        }
        #endregion

        #region Version 10
        [DatabaseVersion(10)]
        private void Migrate10()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("BEGIN TRANSACTION;");

                conn.Execute("CREATE TABLE Folders_backup (" +
                             "FolderID	         INTEGER," +
                             "Path	             TEXT," +
                             "ShowInCollection   INTEGER," +
                             "PRIMARY KEY(FolderID));");

                conn.Execute("INSERT INTO Folders_backup SELECT * FROM Folders;");

                conn.Execute("DROP TABLE Folders;");

                conn.Execute("CREATE TABLE Folders (" +
                             "FolderID	         INTEGER PRIMARY KEY AUTOINCREMENT," +
                             "Path	             TEXT," +
                             "ShowInCollection   INTEGER);");

                conn.Execute("INSERT INTO Folders SELECT * FROM Folders_backup;");

                conn.Execute("DROP TABLE Folders_backup;");

                conn.Execute("COMMIT;");

                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 11
        [DatabaseVersion(11)]
        private void Migrate11()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("BEGIN TRANSACTION;");

                conn.Execute("DROP INDEX IF EXISTS ArtistsIndex;");
                conn.Execute("DROP INDEX IF EXISTS GenresIndex;");
                conn.Execute("DROP INDEX IF EXISTS AlbumsIndex;");
                conn.Execute("DROP INDEX IF EXISTS AlbumsYearIndex;");
                conn.Execute("DROP INDEX IF EXISTS TracksArtistIDIndex;");
                conn.Execute("DROP INDEX IF EXISTS TracksAlbumIDIndex;");
                conn.Execute("DROP INDEX IF EXISTS TracksGenreIDIndex;");
                conn.Execute("DROP INDEX IF EXISTS TracksFolderIDIndex;");
                conn.Execute("DROP INDEX IF EXISTS TracksPathIndex;");

                conn.Execute("ALTER TABLE Configurations RENAME TO Configuration;");
                conn.Execute("ALTER TABLE Artists RENAME TO Artist;");
                conn.Execute("ALTER TABLE Genres RENAME TO Genre;");
                conn.Execute("ALTER TABLE Albums RENAME TO Album;");
                conn.Execute("ALTER TABLE Playlists RENAME TO Playlist;");
                conn.Execute("ALTER TABLE PlaylistEntries RENAME TO PlaylistEntry;");
                conn.Execute("ALTER TABLE Folders RENAME TO Folder;");
                conn.Execute("ALTER TABLE Tracks RENAME TO Track;");
                conn.Execute("ALTER TABLE RemovedTracks RENAME TO RemovedTrack;");
                conn.Execute("ALTER TABLE QueuedTracks RENAME TO QueuedTrack;");
                conn.Execute("ALTER TABLE IndexingStatistics RENAME TO IndexingStatistic;");

                conn.Execute("CREATE INDEX ArtistIndex ON Artist(ArtistName)");
                conn.Execute("CREATE INDEX GenreIndex ON Genre(GenreName)");
                conn.Execute("CREATE INDEX AlbumIndex ON Album(AlbumTitle, AlbumArtist)");
                conn.Execute("CREATE INDEX AlbumYearIndex ON Album(Year);");
                conn.Execute("CREATE INDEX TrackArtistIDIndex ON Track(ArtistID);");
                conn.Execute("CREATE INDEX TrackAlbumIDIndex ON Track(AlbumID);");
                conn.Execute("CREATE INDEX TrackGenreIDIndex ON Track(GenreID);");
                conn.Execute("CREATE INDEX TrackFolderIDIndex ON Track(FolderID);");
                conn.Execute("CREATE INDEX TrackPathIndex ON Track(Path)");

                conn.Execute("COMMIT;");
                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 12
        [DatabaseVersion(12)]
        private void Migrate12()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("BEGIN TRANSACTION;");

                conn.Execute("ALTER TABLE Track ADD SafePath TEXT;");
                conn.Execute("UPDATE Track SET SafePath=LOWER(Path);");

                conn.Execute("CREATE INDEX TrackSafePathIndex ON Track(SafePath);");

                conn.Execute("ALTER TABLE Folder ADD SafePath TEXT;");
                conn.Execute("UPDATE Folder SET SafePath=LOWER(Path);");

                conn.Execute("ALTER TABLE RemovedTrack ADD SafePath TEXT;");
                conn.Execute("UPDATE RemovedTrack SET SafePath=LOWER(Path);");

                conn.Execute("ALTER TABLE QueuedTrack ADD SafePath TEXT;");
                conn.Execute("UPDATE QueuedTrack SET SafePath=LOWER(Path);");

                conn.Execute("COMMIT;");
                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 13
        [DatabaseVersion(13)]
        private void Migrate13()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("BEGIN TRANSACTION;");

                conn.Execute("ALTER TABLE Track ADD Love INTEGER;");
                conn.Execute("UPDATE Track SET Love=0;");

                conn.Execute("COMMIT;");
                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 14
        [DatabaseVersion(14)]
        private void Migrate14()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("BEGIN TRANSACTION;");

                conn.Execute("ALTER TABLE QueuedTrack ADD IsPlaying INTEGER;");
                conn.Execute("ALTER TABLE QueuedTrack ADD ProgressSeconds INTEGER;");

                conn.Execute("COMMIT;");
                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 15
        [DatabaseVersion(15)]
        private void Migrate15()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("BEGIN TRANSACTION;");

                conn.Execute("ALTER TABLE Track ADD HasLyrics INTEGER;");
                conn.Execute("ALTER TABLE Track ADD NeedsIndexing INTEGER;");
                conn.Execute("UPDATE Track SET HasLyrics=0;");
                conn.Execute("UPDATE Track SET NeedsIndexing=1;");

                conn.Execute("COMMIT;");
                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 16
        [DatabaseVersion(16)]
        private void Migrate16()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("BEGIN TRANSACTION;");

                conn.Execute("CREATE TABLE TrackStatistic (" +
                             "TrackStatisticID	    INTEGER PRIMARY KEY AUTOINCREMENT," +
                             "Path	                TEXT," +
                             "SafePath	            TEXT," +
                             "Rating	            INTEGER," +
                             "Love	                INTEGER," +
                             "PlayCount	            INTEGER," +
                             "SkipCount	            INTEGER," +
                             "DateLastPlayed        INTEGER);");

                conn.Execute("CREATE INDEX TrackStatisticSafePathIndex ON TrackStatistic(SafePath);");

                conn.Execute("INSERT INTO TrackStatistic(Path,SafePath,Rating,Love,PlayCount,SkipCount,DateLastPlayed) " +
                             "SELECT Path, Safepath, Rating, Love, PlayCount,SkipCount, DateLastPlayed FROM Track;");

                conn.Execute("CREATE TEMPORARY TABLE Track_Backup (" +
                             "TrackID	            INTEGER," +
                             "ArtistID	            INTEGER," +
                             "GenreID	            INTEGER," +
                             "AlbumID	            INTEGER," +
                             "FolderID	            INTEGER," +
                             "Path	                TEXT," +
                             "SafePath	            TEXT," +
                             "FileName	            TEXT," +
                             "MimeType	            TEXT," +
                             "FileSize	            INTEGER," +
                             "BitRate	            INTEGER," +
                             "SampleRate	        INTEGER," +
                             "TrackTitle	        TEXT," +
                             "TrackNumber	        INTEGER," +
                             "TrackCount	        INTEGER," +
                             "DiscNumber	        INTEGER," +
                             "DiscCount	            INTEGER," +
                             "Duration	            INTEGER," +
                             "Year	                INTEGER," +
                             "HasLyrics	            INTEGER," +
                             "DateAdded  	        INTEGER," +
                             "DateLastSynced	    INTEGER," +
                             "DateFileModified	    INTEGER," +
                             "MetaDataHash	        TEXT," +
                             "NeedsIndexing 	    INTEGER," +
                             "PRIMARY KEY(TrackID));");

                conn.Execute("INSERT INTO Track_Backup SELECT TrackID," +
                                     "ArtistID," +
                                     "GenreID," +
                                     "AlbumID," +
                                     "FolderID," +
                                     "Path," +
                                     "SafePath," +
                                     "FileName," +
                                     "MimeType," +
                                     "FileSize," +
                                     "BitRate," +
                                     "SampleRate," +
                                     "TrackTitle," +
                                     "TrackNumber," +
                                     "TrackCount," +
                                     "DiscNumber," +
                                     "DiscCount," +
                                     "Duration," +
                                     "Year," +
                                     "HasLyrics," +
                                     "DateAdded," +
                                     "DateLastSynced," +
                                     "DateFileModified," +
                                     "MetaDataHash," +
                                     "NeedsIndexing " +
                                     "FROM Track;");

                conn.Execute("DROP TABLE Track;");

                conn.Execute("CREATE TABLE Track (" +
                             "TrackID	            INTEGER," +
                             "ArtistID	            INTEGER," +
                             "GenreID	            INTEGER," +
                             "AlbumID	            INTEGER," +
                             "FolderID	            INTEGER," +
                             "Path	                TEXT," +
                             "SafePath	            TEXT," +
                             "FileName	            TEXT," +
                             "MimeType	            TEXT," +
                             "FileSize	            INTEGER," +
                             "BitRate	            INTEGER," +
                             "SampleRate	        INTEGER," +
                             "TrackTitle	        TEXT," +
                             "TrackNumber	        INTEGER," +
                             "TrackCount	        INTEGER," +
                             "DiscNumber	        INTEGER," +
                             "DiscCount	            INTEGER," +
                             "Duration	            INTEGER," +
                             "Year	                INTEGER," +
                             "HasLyrics	            INTEGER," +
                             "DateAdded  	        INTEGER," +
                             "DateLastSynced	    INTEGER," +
                             "DateFileModified	    INTEGER," +
                             "MetaDataHash	        TEXT," +
                             "NeedsIndexing 	    INTEGER," +
                             "PRIMARY KEY(TrackID));");

                conn.Execute("INSERT INTO Track SELECT TrackID," +
                                     "ArtistID," +
                                     "GenreID," +
                                     "AlbumID," +
                                     "FolderID," +
                                     "Path," +
                                     "SafePath," +
                                     "FileName," +
                                     "MimeType," +
                                     "FileSize," +
                                     "BitRate," +
                                     "SampleRate," +
                                     "TrackTitle," +
                                     "TrackNumber," +
                                     "TrackCount," +
                                     "DiscNumber," +
                                     "DiscCount," +
                                     "Duration," +
                                     "Year," +
                                     "HasLyrics," +
                                     "DateAdded," +
                                     "DateLastSynced," +
                                     "DateFileModified," +
                                     "MetaDataHash," +
                                     "NeedsIndexing " +
                                     "FROM Track_Backup;");

                conn.Execute("DROP TABLE Track_Backup;");

                conn.Execute("COMMIT;");
                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 17
        [DatabaseVersion(17)]
        private void Migrate17()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("BEGIN TRANSACTION;");

                conn.Execute("DELETE FROM QueuedTrack;");
                conn.Execute("ALTER TABLE QueuedTrack ADD QueueID TEXT;");

                conn.Execute("COMMIT;");
                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 18
        [DatabaseVersion(18)]
        private void Migrate18()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("BEGIN TRANSACTION;");

                conn.Execute("DROP TABLE Playlist;");
                conn.Execute("DROP TABLE PlaylistEntry;");

                conn.Execute("COMMIT;");
                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region Version 19
        [DatabaseVersion(19)]
        private void Migrate19()
        {
            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("BEGIN TRANSACTION;");

                conn.Execute("ALTER TABLE Album ADD DateCreated INTEGER;");
                conn.Execute("UPDATE Album SET DateCreated=DateAdded;");
                conn.Execute($"UPDATE Album SET DateAdded={DateTime.Now.Ticks};");

                conn.Execute("COMMIT;");
                conn.Execute("VACUUM;");
            }
        }
        #endregion

        #region IDbMigrator
        public bool DatabaseNeedsUpgrade()
        {
            using (var conn = this.factory.GetConnection())
            {
                try
                {
                    this.userDatabaseVersion = Convert.ToInt32(conn.ExecuteScalar<string>("SELECT Value FROM Configuration WHERE Key = 'DatabaseVersion'"));
                }
                catch (Exception)
                {
                    // TODO: in database version 11, the table Configurations was renamed to Configuration. When migrating from version 10 to 11, 
                    // we still need to get the version from the original table as the new Configuration doesn't exist yet and is not found. 
                    // At some later point in time, this try catch can be removed.
                    this.userDatabaseVersion = Convert.ToInt32(conn.ExecuteScalar<string>("SELECT Value FROM Configurations WHERE Key = 'DatabaseVersion'"));
                }
            }

            return this.userDatabaseVersion < CURRENT_VERSION;
        }

        public void InitializeNewDatabase()
        {
            this.CreateConfiguration();
            this.CreateTablesAndIndexes();

            LogClient.Current.Info("New database created at {0}", this.factory.DatabaseFile);
        }

        public virtual void UpgradeDatabase()
        {
            for (int i = this.userDatabaseVersion + 1; i <= CURRENT_VERSION; i++)
            {
                MethodInfo method = typeof(DbMigrator).GetTypeInfo().GetDeclaredMethod("Migrate" + i);
                if (method != null) method.Invoke(this, null);
            }

            using (var conn = this.factory.GetConnection())
            {
                conn.Execute("UPDATE Configuration SET Value = ? WHERE Key = 'DatabaseVersion'", CURRENT_VERSION);
            }

            LogClient.Current.Info("Migrated from database version {0} to {1}", this.userDatabaseVersion.ToString(), CURRENT_VERSION.ToString());
        }
        #endregion

        #region Abstract
        public abstract bool DatabaseExists();
        #endregion
    }
}