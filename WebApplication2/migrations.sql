IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Projects] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [UserId] int NOT NULL,
    CONSTRAINT [PK_Projects] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [Email] nvarchar(450) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260409091004_init', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260409091149_addTables', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [Bugs] (
    [Id] int NOT NULL IDENTITY,
    [BugId] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Url] nvarchar(max) NOT NULL,
    [ExpectedResult] nvarchar(max) NOT NULL,
    [ActualResult] nvarchar(max) NOT NULL,
    [Note] nvarchar(max) NOT NULL,
    [Priority] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedAt] bigint NOT NULL,
    [ProjectId] int NOT NULL,
    CONSTRAINT [PK_Bugs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Bugs_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Bugs_ProjectId] ON [Bugs] ([ProjectId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260409105205_addedBugs', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [ProjectMembers] (
    [Id] int NOT NULL IDENTITY,
    [ProjectId] int NOT NULL,
    [UserId] int NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_ProjectMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProjectMembers_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProjectMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_ProjectMembers_ProjectId] ON [ProjectMembers] ([ProjectId]);

CREATE INDEX [IX_ProjectMembers_UserId] ON [ProjectMembers] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260409124933_addProjectMember', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [ProjectMembers] ADD [Status] nvarchar(max) NOT NULL DEFAULT N'';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260410092730_addInviteNotification', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Bugs] ADD [AttachmentUrl] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260410105429_AddAttachmentUrl', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260410144023_CreateProject', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260412093836_AddAttachmentUrlImage', N'10.0.5');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [ProjectMembers] ADD [InvitedById] int NULL;

CREATE INDEX [IX_ProjectMembers_InvitedById] ON [ProjectMembers] ([InvitedById]);

ALTER TABLE [ProjectMembers] ADD CONSTRAINT [FK_ProjectMembers_Users_InvitedById] FOREIGN KEY ([InvitedById]) REFERENCES [Users] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260413085434_InvitedBy', N'10.0.5');

COMMIT;
GO

