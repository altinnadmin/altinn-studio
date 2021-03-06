using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using AltinnCore.Common.Configuration;
using AltinnCore.Common.Models;
using AltinnCore.Common.Services.Interfaces;
using AltinnCore.RepositoryClient.Model;
using AltinnCore.ServiceLibrary.Configuration;
using AltinnCore.ServiceLibrary.ServiceMetadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AltinnCore.Designer.Controllers
{
    /// <summary>
    /// This is the API controller for functionality related to repositories.
    /// </summary>
    [Authorize]
    public class RepositoryController : ControllerBase
    {
        private readonly IGitea _giteaApi;
        private readonly ServiceRepositorySettings _settings;
        private readonly ISourceControl _sourceControl;
        private readonly IRepository _repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryController"/> class.
        /// </summary>
        /// <param name="giteaWrapper">the gitea wrapper</param>
        /// <param name="repositorySettings">Settings for repository</param>
        /// <param name="sourceControl">the source control</param>
        /// <param name="repository">the repository control</param>
        public RepositoryController(IGitea giteaWrapper, IOptions<ServiceRepositorySettings> repositorySettings, ISourceControl sourceControl, IRepository repository)
        {
            _giteaApi = giteaWrapper;
            _settings = repositorySettings.Value;
            _sourceControl = sourceControl;
            _repository = repository;
        }

        /// <summary>
        /// Returns a list over repositories
        /// </summary>
        /// <param name="repositorySearch">The search params</param>
        /// <returns>List of repostories that user has access to.</returns>
        [HttpGet]
        public List<Repository> Search(RepositorySearch repositorySearch)
        {
            SearchResults repositorys = _giteaApi.SearchRepository(repositorySearch.OnlyAdmin, repositorySearch.KeyWord, repositorySearch.Page).Result;
            return repositorys.Data;
        }

        /// <summary>
        /// List of all organizations a user has access to.
        /// </summary>
        /// <returns>A list over all organizations user has access to</returns>
        [HttpGet]
        public List<Organization> Organizations()
        {
            List<Organization> orglist = _giteaApi.GetUserOrganizations().Result;
            return orglist;
        }

        /// <summary>
        /// Returns a specic organization
        /// </summary>
        /// <param name="id">The organization name</param>
        /// <returns>The organization</returns>
        [HttpGet]
        public ActionResult<Organization> Organization(string id)
        {
            Organization org = _giteaApi.GetOrganization(id).Result;
            if (org != null)
            {
                return org;
            }

            return NotFound();
        }

        /// <summary>
        /// This method returns the status of a given repository
        /// </summary>
        /// <param name="owner">The organization or user owning the repo</param>
        /// <param name="repository">The repository</param>
        /// <returns>The repository status</returns>
        [HttpGet]
        public RepoStatus RepoStatus(string owner, string repository)
        {
            _sourceControl.FetchRemoteChanges(owner, repository);
            return _sourceControl.RepositoryStatus(owner, repository);
        }

        /// <summary>
        /// Pull remote changes for a given repo
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">Name of the repository</param>
        /// <returns>Repo status</returns>
        [HttpGet]
        public RepoStatus Pull(string owner, string repository)
        {
            RepoStatus pullStatus = _sourceControl.PullRemoteChanges(owner, repository);

            RepoStatus status = _sourceControl.RepositoryStatus(owner, repository);

            if (pullStatus.RepositoryStatus != Common.Enums.RepositoryStatus.Ok)
            {
                status.RepositoryStatus = pullStatus.RepositoryStatus;
            }

            return status;
        }

        /// <summary>
        /// Pushes changes for a given repo
        /// </summary>
        /// <param name="commitInfo">Info about the commit</param>
        [HttpPost]
        public void CommitAndPushRepo([FromBody]CommitInfo commitInfo)
        {
            _sourceControl.PushChangesForRepository(commitInfo);
        }

        /// <summary>
        /// Commit changes
        /// </summary>
        /// <param name="commitInfo">Info about the commit</param>
        /// <returns>http response message as ok if commit is successfull</returns>
        [HttpPost]
        public ActionResult<HttpResponseMessage> Commit([FromBody]CommitInfo commitInfo)
        {
            try
            {
                _sourceControl.Commit(commitInfo);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Push commits to repo
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The repo name</param>
        [HttpPost]
        public void Push(string owner, string repository)
        {
            _sourceControl.Push(owner, repository);
        }

        /// <summary>
        /// Push commits to repo
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The repo name</param>
        /// <returns>List of commits</returns>
        [HttpGet]
        public List<Commit> Log(string owner, string repository)
        {
            return _sourceControl.Log(owner, repository);
        }

        /// <summary>
        /// Push commits to repo
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The repo name</param>
        /// <returns>List of commits</returns>
        [HttpGet]
        public Commit GetLatestCommitFromCurrentUser(string owner, string repository)
        {
            return _sourceControl.GetLatestCommitForCurrentUser(owner, repository);
        }

        /// <summary>
        /// List all branches for a repository
        /// </summary>
        /// <param name="owner">The owner of the repo</param>
        /// <param name="repository">The repository</param>
        /// <returns>List of repos</returns>
        [HttpGet]
        public List<Branch> Branches(string owner, string repository)
        {
            return _giteaApi.GetBranches(owner, repository).Result;
        }

        /// <summary>
        /// Returns information about a given branch
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of repository</param>
        /// <param name="branch">Name of branch</param>
        /// <returns>The branch info</returns>
        [HttpGet]
        public Branch Branch(string owner, string repository, string branch)
        {
            return _giteaApi.GetBranch(owner, repository, branch).Result;
        }

        /// <summary>
        /// Discards all local changes for the logged in user and the local repository is updated with latest remote commit (origin/master)
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of repository</param>
        /// <returns>Http response message as ok if reset operation is successful</returns>
        [HttpGet]
        public ActionResult<HttpResponseMessage> DiscardLocalChanges(string owner, string repository)
        {
            try
            {
                if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repository))
                {
                    HttpResponseMessage badRequest = new HttpResponseMessage(HttpStatusCode.BadRequest);
                    badRequest.ReasonPhrase = "One or all of the input parameters are null";
                    return badRequest;
                }

                _sourceControl.ResetCommit(owner, repository);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Discards local changes to a specific file and the files is updated with latest remote commit (origin/master)
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of repository</param>
        /// <param name="fileName">the name of the file</param>
        /// <returns>Http response message as ok if checkout operation is successful</returns>
        [HttpGet]
        public ActionResult<HttpResponseMessage> DiscardLocalChangesForSpecificFile(string owner, string repository, string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repository) || string.IsNullOrEmpty(fileName))
                {
                    HttpResponseMessage badRequest = new HttpResponseMessage(HttpStatusCode.BadRequest);
                    badRequest.ReasonPhrase = "One or all of the input parameters are null";
                    return badRequest;
                }

                _sourceControl.CheckoutLatestCommitForSpecificFile(owner, repository, fileName);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Stages a specific file changed in working repository.
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of repository</param>
        /// <param name="fileName">the entire file path with filen name</param>
        /// <returns>Http response message as ok if checkout operation is successful</returns>
        [HttpGet]
        public ActionResult<HttpResponseMessage> StageChange(string owner, string repository, string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repository) || string.IsNullOrEmpty(fileName))
                {
                    HttpResponseMessage badRequest = new HttpResponseMessage(HttpStatusCode.BadRequest);
                    badRequest.ReasonPhrase = "One or all of the input parameters are null";
                    return badRequest;
                }

                _sourceControl.StageChange(owner, repository, fileName);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Action used to create a new service under the current service owner
        /// </summary>
        /// <param name="org">The service owner code</param>
        /// <param name="serviceName">The name of the service to create</param>
        /// <param name="repoName">The repository name of the service to create</param>
        /// <returns>
        /// An indication if service was created successful or not
        /// </returns>
        [Authorize]
        [HttpPost]
        public Repository CreateService(string org, string serviceName, string repoName)
        {
            ServiceConfiguration serviceConfiguration = new ServiceConfiguration
            {
                RepositoryName = repoName,
                ServiceName = serviceName,
            };

            string serviceName1 = serviceConfiguration.RepositoryName;
            IList<ServiceConfiguration> services = _repository.GetServices(org);
            List<string> serviceNames = services.Select(c => c.RepositoryName.ToLower()).ToList();
            bool serviceNameAlreadyExists = serviceNames.Contains(serviceName1.ToLower());

            if (!serviceNameAlreadyExists)
            {
                return _repository.CreateService(org, serviceConfiguration);
            }
            else
            {
                return new Repository()
                {
                    RepositoryCreatedStatus = System.Net.HttpStatusCode.UnprocessableEntity,
                };
            }
        }

        /// <summary>
        /// Clones the remote repository
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of repository</param>
        /// <returns>The result of the cloning</returns>
        [HttpGet]
        public string CloneRemoteRepository(string owner, string repository)
        {
            return _sourceControl.CloneRemoteRepository(owner, repository);
        }

        /// <summary>
        /// Halts the merge operation and keeps local changes
        /// </summary>
        /// <param name="owner">The owner of the repository</param>
        /// <param name="repository">The name of the repository</param>
        /// <returns>Http response message as ok if abort merge operation is successful</returns>
        [HttpGet]
        public ActionResult<HttpResponseMessage> AbortMerge(string owner, string repository)
        {
            try
            {
                if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repository))
                {
                    HttpResponseMessage badRequest = new HttpResponseMessage(HttpStatusCode.BadRequest);
                    badRequest.ReasonPhrase = "One or all of the input parameters are null";
                    return badRequest;
                }

                _sourceControl.AbortMerge(owner, repository);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }
    }
}
