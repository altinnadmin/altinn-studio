using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using AltinnCore.Common.Configuration;
using AltinnCore.Common.Helpers;
using AltinnCore.Common.Models;
using AltinnCore.Common.Services.Interfaces;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AltinnCore.Common.Services.Implementation
{
    /// <summary>
    /// Implmentation for source control
    /// </summary>
    public class SourceControlSI : ISourceControl
    {
        private readonly IDefaultFileFactory _defaultFileFactory;
        private readonly ServiceRepositorySettings _settings;
        private readonly GeneralSettings _generalSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IGitea _gitea;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceControlSI"/> class
        /// </summary>
        /// <param name="repositorySettings">The settings for the service repository</param>
        /// <param name="generalSettings">The current general settings</param>
        /// <param name="defaultFileFactory">The default factory</param>
        /// <param name="httpContextAccessor">the http context accessor</param>
        /// <param name="gitea">gitea</param>
        public SourceControlSI(
            IOptions<ServiceRepositorySettings> repositorySettings,
            IOptions<GeneralSettings> generalSettings,
            IDefaultFileFactory defaultFileFactory,
            IHttpContextAccessor httpContextAccessor,
            IGitea gitea)
        {
            _defaultFileFactory = defaultFileFactory;
            _settings = repositorySettings.Value;
            _generalSettings = generalSettings.Value;
            _httpContextAccessor = httpContextAccessor;
            _gitea = gitea;
        }

        /// <summary>
        /// Clone remote repository
        /// </summary>
        /// <param name="org">the organisation</param>
        /// <param name="repository">the name of the repository</param>
        /// <returns>The result of the cloning</returns>
        public string CloneRemoteRepository(string org, string repository)
        {
            string remoteRepo = FindRemoteRepoLocation(org, repository);
            CloneOptions cloneOptions = new CloneOptions();
            cloneOptions.CredentialsProvider = (a, b, c) => new UsernamePasswordCredentials { Username = GetAppToken(), Password = string.Empty };
            return Repository.Clone(remoteRepo, FindLocalRepoLocation(org, repository), cloneOptions);
        }

        /// <summary>
        /// Verifies if developer has a local repo
        /// </summary>
        /// <param name="org">the organisation</param>
        /// <param name="service">the service</param>
        /// <returns>A bool indicating if the repository is a local one or not</returns>
        public bool IsLocalRepo(string org, string service)
        {
            string localServiceRepoFolder = _settings.GetServicePath(org, service, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            if (Directory.Exists(localServiceRepoFolder))
            {
                try
                {
                    using (Repository repo = new Repository(localServiceRepoFolder))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Pulls remote changes
        /// </summary>
        /// <param name="owner">Owner of the repository</param>
        /// <param name="repository">The repository</param>
        /// <returns>The repo status</returns>
        public RepoStatus PullRemoteChanges(string owner, string repository)
        {
            RepoStatus status = new RepoStatus();

            using (var repo = new Repository(FindLocalRepoLocation(owner, repository)))
            {
                PullOptions pullOptions = new PullOptions()
                {
                    MergeOptions = new MergeOptions()
                    {
                        FastForwardStrategy = FastForwardStrategy.Default,
                    },
                };

                pullOptions.FetchOptions = new FetchOptions();
                pullOptions.FetchOptions.CredentialsProvider = (_url, _user, _cred) =>
                        new UsernamePasswordCredentials { Username = GetAppToken(), Password = string.Empty };

                try
                {
                    MergeResult mergeResult = Commands.Pull(
                        repo,
                        new LibGit2Sharp.Signature("my name", "my email", DateTimeOffset.Now), // I dont want to provide these
                        pullOptions);

                    if (mergeResult.Status == MergeStatus.Conflicts)
                    {
                        status.RepositoryStatus = Enums.RepositoryStatus.MergeConflict;
                    }
                }
                catch (LibGit2Sharp.CheckoutConflictException)
                {
                    status.RepositoryStatus = Enums.RepositoryStatus.CheckoutConflict;
                }
            }

            return status;
        }

        /// <summary>
        /// Fetches the remote changes
        /// </summary>
        /// <param name="org">the organisation</param>
        /// <param name="repository">the repository</param>
        public void FetchRemoteChanges(string org, string repository)
        {
            string logMessage = string.Empty;
            using (var repo = new Repository(FindLocalRepoLocation(org, repository)))
            {
                FetchOptions fetchOptions = new FetchOptions();
                fetchOptions.CredentialsProvider = (_url, _user, _cred) =>
                         new UsernamePasswordCredentials { Username = GetAppToken(), Password = string.Empty };

                foreach (Remote remote in repo.Network.Remotes)
                {
                    IEnumerable<string> refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                    Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, logMessage);
                }
            }
        }

        /// <summary>
        /// Gets the number of commits the local repository is behind the remote
        /// </summary>
        /// <param name="org">The organization owning the repository</param>
        /// <param name="repository">The repository</param>
        /// <returns>The number of commits behind</returns>
        public int? CheckRemoteUpdates(string org, string repository)
        {
            using (var repo = new Repository(FindLocalRepoLocation(org, repository)))
            {
                Branch branch = repo.Branches["master"];
                if (branch == null)
                {
                    return null;
                }

                return branch.TrackingDetails.BehindBy;
            }
        }

        /// <summary>
        /// Add all changes in service repo and push to remote
        /// </summary>
        /// <param name="commitInfo">the commit information for the service</param>
        public void PushChangesForRepository(CommitInfo commitInfo)
        {
            string localServiceRepoFolder = _settings.GetServicePath(commitInfo.Org, commitInfo.Repository, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            using (Repository repo = new Repository(localServiceRepoFolder))
            {
                // Restrict users from empty commit
                if (repo.RetrieveStatus().IsDirty)
                {
                    string remoteUrl = FindRemoteRepoLocation(commitInfo.Org, commitInfo.Repository);
                    Remote remote = repo.Network.Remotes["origin"];

                    if (!remote.PushUrl.Equals(remoteUrl))
                    {
                        // This is relevant when we switch beteen running designer in local or in docker. The remote URL changes.
                        // Requires adminstrator access to update files.
                        repo.Network.Remotes.Update("origin", r => r.Url = remoteUrl);
                    }

                    Commands.Stage(repo, "*");

                    // Create the committer's signature and commit
                    LibGit2Sharp.Signature author = new LibGit2Sharp.Signature(AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext), "@jugglingnutcase", DateTime.Now);
                    LibGit2Sharp.Signature committer = author;

                    // Commit to the repository
                    LibGit2Sharp.Commit commit = repo.Commit(commitInfo.Message, author, committer);

                    PushOptions options = new PushOptions();
                    options.CredentialsProvider = (_url, _user, _cred) =>
                        new UsernamePasswordCredentials { Username = GetAppToken(), Password = string.Empty };
                    repo.Network.Push(remote, @"refs/heads/master", options);
                }
            }
        }

        /// <summary>
        /// Push commits to repository
        /// </summary>
        /// <param name="owner">The owner of the repo</param>
        /// <param name="repository">The repository</param>
        public void Push(string owner, string repository)
        {
            string localServiceRepoFolder = _settings.GetServicePath(owner, repository, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            using (Repository repo = new Repository(localServiceRepoFolder))
            {
                string remoteUrl = FindRemoteRepoLocation(owner, repository);
                Remote remote = repo.Network.Remotes["origin"];

                if (!remote.PushUrl.Equals(remoteUrl))
                {
                    // This is relevant when we switch beteen running designer in local or in docker. The remote URL changes.
                    // Requires adminstrator access to update files.
                    repo.Network.Remotes.Update("origin", r => r.Url = remoteUrl);
                }

                PushOptions options = new PushOptions();
                options.CredentialsProvider = (_url, _user, _cred) =>
                        new UsernamePasswordCredentials { Username = GetAppToken(), Password = string.Empty };

                repo.Network.Push(remote, @"refs/heads/master", options);
            }
        }

        /// <summary>
        /// Commit changes for repository
        /// </summary>
        /// <param name="commitInfo">Information about the commit</param>
        public void Commit(CommitInfo commitInfo)
        {
            string localServiceRepoFolder = _settings.GetServicePath(commitInfo.Org, commitInfo.Repository, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            using (Repository repo = new Repository(localServiceRepoFolder))
            {
                string remoteUrl = FindRemoteRepoLocation(commitInfo.Org, commitInfo.Repository);
                Remote remote = repo.Network.Remotes["origin"];

                if (!remote.PushUrl.Equals(remoteUrl))
                {
                    // This is relevant when we switch beteen running designer in local or in docker. The remote URL changes.
                    // Requires adminstrator access to update files.
                    repo.Network.Remotes.Update("origin", r => r.Url = remoteUrl);
                }

                Commands.Stage(repo, "*");

                // Create the committer's signature and commit
                LibGit2Sharp.Signature author = new LibGit2Sharp.Signature(AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext), "@jugglingnutcase", DateTime.Now);
                LibGit2Sharp.Signature committer = author;

                // Commit to the repository
                LibGit2Sharp.Commit commit = repo.Commit(commitInfo.Message, author, committer);
            }
        }

        /// <summary>
        /// List the GIT status of a repository
        /// </summary>
        /// <param name="org">The organization owning the repository</param>
        /// <param name="repository">The name of the repository</param>
        /// <returns>A list of changed files in the repository</returns>
        public List<RepositoryContent> Status(string org, string repository)
        {
            List<RepositoryContent> repoContent = new List<RepositoryContent>();
            string localServiceRepoFolder = _settings.GetServicePath(org, repository, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            using (var repo = new Repository(localServiceRepoFolder))
            {
                RepositoryStatus status = repo.RetrieveStatus(new LibGit2Sharp.StatusOptions());
                foreach (StatusEntry item in status)
                {
                    RepositoryContent content = new RepositoryContent();
                    content.FilePath = item.FilePath;
                    repoContent.Add(content);
                }
            }

            return repoContent;
        }

        /// <summary>
        /// Gives the complete repository status
        /// </summary>
        /// <param name="owner">The owner of the repo, org or user</param>
        /// <param name="repository">The name of repository</param>
        /// <returns>The repository status</returns>
        public RepoStatus RepositoryStatus(string owner, string repository)
        {
            RepoStatus repoStatus = new RepoStatus();
            repoStatus.ContentStatus = new List<RepositoryContent>();
            string localServiceRepoFolder = _settings.GetServicePath(owner, repository, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            using (var repo = new Repository(localServiceRepoFolder))
            {
                RepositoryStatus status = repo.RetrieveStatus(new LibGit2Sharp.StatusOptions());
                foreach (StatusEntry item in status)
                {
                    RepositoryContent content = new RepositoryContent();
                    content.FilePath = item.FilePath;
                    content.FileStatus = (AltinnCore.Common.Enums.FileStatus)(int)item.State;
                    if (content.FileStatus == Enums.FileStatus.Conflicted)
                    {
                        repoStatus.RepositoryStatus = Enums.RepositoryStatus.MergeConflict;
                        repoStatus.HasMergeConflict = true;
                    }

                    repoStatus.ContentStatus.Add(content);
                }

                Branch branch = repo.Branches.FirstOrDefault(b => b.IsTracking == true);
                if (branch != null)
                {
                    repoStatus.AheadBy = branch.TrackingDetails.AheadBy;
                    repoStatus.BehindBy = branch.TrackingDetails.BehindBy;
                }
            }

            return repoStatus;
        }

        /// <summary>
        /// Gets the latest commit for current user
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of the repository</param>
        /// <returns>The latest commit</returns>
        public AltinnCore.Common.Models.Commit GetLatestCommitForCurrentUser(string owner, string repository)
        {
            List<AltinnCore.Common.Models.Commit> commits = Log(owner, repository);
            var currentUser = AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext);

            return commits.FirstOrDefault(commit => commit.Author.Name == currentUser);
        }

        /// <summary>
        /// List commits
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of the repository</param>
        /// <returns>List of commits</returns>
        public List<AltinnCore.Common.Models.Commit> Log(string owner, string repository)
        {
            List<AltinnCore.Common.Models.Commit> commits = new List<Models.Commit>();
            string localServiceRepoFolder = _settings.GetServicePath(owner, repository, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            using (var repo = new Repository(localServiceRepoFolder))
            {
                foreach (LibGit2Sharp.Commit c in repo.Commits.Take(50))
                {
                    Models.Commit commit = new Models.Commit();
                    commit.Message = c.Message;
                    commit.MessageShort = c.MessageShort;
                    commit.Encoding = c.Encoding;
                    commit.Sha = c.Sha;

                    commit.Author = new Models.Signature();
                    commit.Author.Email = c.Author.Email;
                    commit.Author.Name = c.Author.Name;
                    commit.Author.When = c.Author.When;

                    commit.Commiter = new Models.Signature();
                    commit.Commiter.Name = c.Committer.Name;
                    commit.Commiter.Email = c.Committer.Email;
                    commit.Commiter.When = c.Committer.When;

                    commits.Add(commit);
                }
            }

            return commits;
        }

        /// <summary>
        /// Creates the remote repository
        /// </summary>
        /// <param name="owner">The owner</param>
        /// <param name="createRepoOption">Options for the remote repository</param>
        /// <returns>The repostory from API</returns>
        public AltinnCore.RepositoryClient.Model.Repository CreateRepository(string owner, AltinnCore.RepositoryClient.Model.CreateRepoOption createRepoOption)
        {
            return _gitea.CreateRepository(owner, createRepoOption).Result;
        }

        /// <summary>
        /// Method for storing AppToken in Developers folder. This is not the permanent solution
        /// </summary>
        /// <param name="token">The</param>
        public void StoreAppTokenForUser(string token)
        {
            CheckAndCreateDeveloperFolder();
            string path = null;
            if (Environment.GetEnvironmentVariable("ServiceRepositorySettings__RepositoryLocation") != null)
            {
                path = Environment.GetEnvironmentVariable("ServiceRepositorySettings__RepositoryLocation") + AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext) + "/AuthToken.txt";
            }
            else
            {
                path = _settings.RepositoryLocation + AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext) + "/AuthToken.txt";
            }

            File.WriteAllText(path, token);
        }

        /// <summary>
        /// Return the App Token generated to let AltinnCore contact GITEA on behalf of service developer
        /// </summary>
        /// <returns>The app token</returns>
        public string GetAppToken()
        {
            return AuthenticationHelper.GetDeveloperAppToken(_httpContextAccessor.HttpContext);
        }

        /// <summary>
        /// Verifies if there exist a developer folder
        /// </summary>
        private void CheckAndCreateDeveloperFolder()
        {
            string path = null;
            if (Environment.GetEnvironmentVariable("ServiceRepositorySettings__RepositoryLocation") != null)
            {
                path = Environment.GetEnvironmentVariable("ServiceRepositorySettings__RepositoryLocation") + AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext) + "/";
            }
            else
            {
                path = _settings.RepositoryLocation + AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext) + "/";
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Returns the local
        /// </summary>
        /// <param name="org">The organization owning the repostory</param>
        /// <param name="repository">The name of the repository</param>
        /// <returns>The path to the local repository</returns>
        public string FindLocalRepoLocation(string org, string repository)
        {
            string localpath = null;
            if (Environment.GetEnvironmentVariable("ServiceRepositorySettings__RepositoryLocation") != null)
            {
                localpath = $"{Environment.GetEnvironmentVariable("ServiceRepositorySettings__RepositoryLocation")}{AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext)}/{org}/{repository}";
            }
            else
            {
                localpath = $"{_settings.RepositoryLocation}{AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext)}/{org}/{repository}";
            }

            return localpath;
        }

        /// <summary>
        /// Returns the remote repo
        /// </summary>
        /// <param name="org">The organization owning the repository</param>
        /// <param name="repository">The repository</param>
        /// <returns>The path to the remote repo</returns>
        private string FindRemoteRepoLocation(string org, string repository)
        {
            if (Environment.GetEnvironmentVariable("ServiceRepositorySettings__RepositoryBaseURL") != null)
            {
                return $"{Environment.GetEnvironmentVariable("ServiceRepositorySettings__RepositoryBaseURL")}{org}/{repository}.git";
            }
            else
            {
                return $"{_settings.RepositoryBaseURL}{org}/{repository}.git";
            }
        }

        /// <summary>
        /// Discards all local changes for the logged in user and the local repository is updated with latest remote commit (origin/master)
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of the repository</param>
        public void ResetCommit(string owner, string repository)
        {
            string localServiceRepoFolder = _settings.GetServicePath(owner, repository, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            using (Repository repo = new Repository(localServiceRepoFolder))
            {
                if (repo.RetrieveStatus().IsDirty)
                {
                    repo.Reset(ResetMode.Hard, "origin/master");
                    repo.RemoveUntrackedFiles();
                }
            }
        }

        /// <summary>
        /// Discards local changes to a specific file and the file is updated with latest remote commit (origin/master)
        /// by checking out the specific file.
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of the repository</param>
        /// <param name="fileName">the name of the file</param>
        public void CheckoutLatestCommitForSpecificFile(string owner, string repository, string fileName)
        {
            string localServiceRepoFolder = _settings.GetServicePath(owner, repository, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            using (Repository repo = new Repository(localServiceRepoFolder))
            {
                CheckoutOptions checkoutOptions = new CheckoutOptions
                {
                    CheckoutModifiers = CheckoutModifiers.Force,
                };

                repo.CheckoutPaths("origin/master", new[] { fileName }, checkoutOptions);
            }
        }

        /// <summary>
        /// Stages a specific file changed in working repository.
        /// </summary>
        /// <param name="owner">The owner of the repository.</param>
        /// <param name="repository">The name of the repository.</param>
        /// <param name="fileName">the entire file path with filen name</param>        
        public void StageChange(string owner, string repository, string fileName)
        {
            string localServiceRepoFolder = _settings.GetServicePath(owner, repository, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            using (Repository repo = new Repository(localServiceRepoFolder))
            {
                FileStatus fileStatus = repo.RetrieveStatus().SingleOrDefault(file => file.FilePath == fileName).State;

                if (fileStatus == FileStatus.ModifiedInWorkdir ||
                    fileStatus == FileStatus.NewInWorkdir ||
                    fileStatus == FileStatus.Conflicted)
                {
                    Commands.Stage(repo, fileName);
                }
            }
        }

        /// <summary>
        /// Halts the merge operation and keeps local changes.
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of the repository</param>
        public void AbortMerge(string owner, string repository)
        {
            string localServiceRepoFolder = _settings.GetServicePath(owner, repository, AuthenticationHelper.GetDeveloperUserName(_httpContextAccessor.HttpContext));
            using (Repository repo = new Repository(localServiceRepoFolder))
            {
                if (repo.RetrieveStatus().IsDirty)
                {
                    repo.Reset(ResetMode.Hard, "heads/master");
                }
            }
        }
    }
}
