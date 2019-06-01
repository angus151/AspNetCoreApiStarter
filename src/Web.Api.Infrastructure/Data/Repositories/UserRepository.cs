﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Web.Api.Core.Domain.Entities;
using Web.Api.Core.Dto;
using Web.Api.Core.Dto.GatewayResponses.Repositories;
//using Web.Api.Core.Dto.UseCaseResponses;
using Web.Api.Core.Interfaces.Gateways.Repositories;
using Web.Api.Core.Specifications;
using Web.Api.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
namespace Web.Api.Infrastructure.Data.Repositories
{
    internal sealed class UserRepository : EfRepository<User>, IUserRepository
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IMapper _mapper;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(ILogger<UserRepository> logger, UserManager<AppUser> userManager, IMapper mapper, AppDbContext appDbContext): base(appDbContext)
        {
            _userManager = userManager;
            _mapper = mapper;
        }

        public async Task<CreateUserResponse> Create(string firstName, string lastName, string email, string userName, string password)
        {
            try
            {
                var appUser = new AppUser { Email = email, UserName = userName, FirstName = firstName, LastName = lastName };
                var identityResult = await _userManager.CreateAsync(appUser, password);
                if (!identityResult.Succeeded)
                    return new CreateUserResponse(appUser.Id, false, identityResult.Errors.Select(e => new Error(e.Code, e.Description)).ToList());
                // add the email claim and value for this user
                await _userManager.AddClaimAsync(appUser, new Claim(ClaimTypes.Name, appUser.UserName));
                var user = new User(firstName, lastName, appUser.Id.ToString(), appUser.UserName);
                _appDbContext.Users.Add(user);
                await _appDbContext.SaveChangesAsync();
                return new CreateUserResponse(appUser.Id, identityResult.Succeeded, identityResult.Succeeded ? null : identityResult.Errors.Select(e => new Error(e.Code, e.Description)).ToList());
            } catch (Exception e) {
                return new CreateUserResponse(null, false, new List<Error>() { new Error(null, e.Message) });
            }
        }
        public async Task<DeleteUserResponse> Delete(string userName)
        {
            try
            {
                var appUser = await _userManager.FindByNameAsync(userName);
                if (appUser != null)
                {
                    var identityResult = await _userManager.DeleteAsync(appUser);
                    if (!identityResult.Succeeded)
                        return new DeleteUserResponse(appUser.Id, false, identityResult.Errors.Select(e => new Error(e.Code, e.Description)).ToList());
                    User user = _mapper.Map(appUser, await GetSingleBySpec(new UserSpecification(appUser.Id)), opt => opt.ConfigureMap(MemberList.None));
                    if (user != null)
                    {
                        _appDbContext.Users.Remove(user);
                        await _appDbContext.SaveChangesAsync();
                    } else
                        return new DeleteUserResponse(null, false, new List<Error>() { new Error(null, "Failed to remove user from app DB! This is result in inconsistency between application Users table and Identity framework!") });
                    return new DeleteUserResponse(appUser.Id, identityResult.Succeeded, identityResult.Succeeded ? null : identityResult.Errors.Select(e => new Error(e.Code, e.Description)).ToList());
                } else
                    return new DeleteUserResponse(null, false, new List<Error>() { new Error(null, "Invalid user!") });
            }
            catch (Exception e)
            {
                return new DeleteUserResponse(null, false, new List<Error>() { new Error(null, e.Message) });
            }
        }
        public async Task<User> FindUserByName(string userName)
        {
            try
            {
                var appUser = await _userManager.FindByNameAsync(userName);
                return appUser == null ? null : _mapper.Map(appUser, await GetSingleBySpec(new UserSpecification(appUser.Id)), opt => opt.ConfigureMap(MemberList.None));
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<FindUserResponse> FindByName(string userName)
        {
            try
            {
                return await getUser(await _userManager.FindByNameAsync(userName));
            } catch (Exception e)
            {
                return null;
            }
        }
        public async Task<FindUserResponse> FindById(string id)
        {
            try
            {
                return await getUser(await _userManager.FindByIdAsync(id));
            }
            catch (Exception e)
            {
                return null;
            }
        }
        public async Task<FindUserResponse> FindByEmail(string email)
        {
            try
            {
                return await getUser(await _userManager.FindByEmailAsync(email));
            }
            catch (Exception e)
            {
                return null;
            }
        }
        public async Task<bool> CheckPassword(User user, string password)
        {
            AppUser appUser = _mapper.Map<AppUser>(user);
            return await _userManager.CheckPasswordAsync(_mapper.Map<AppUser>(user), password);
        }
        public async Task<PasswordResponse> ChangePassword(string id, string oldPassword, string newPassword)
        {
            try {
                AppUser appUser = await _userManager.FindByIdAsync(id);
                if (appUser == null)
                    return new PasswordResponse(null, false, new List<Error>(){new Error(null, "User not found!")});
                IdentityResult identityResult = await _userManager.ChangePasswordAsync(appUser, oldPassword, newPassword);
                if (identityResult.Succeeded)
                    return new PasswordResponse(appUser.Id, true, null);
                else {
                    _logger.LogError($"{nameof(ChangePassword)} failed to change password of user {id}!");
                    return new PasswordResponse(appUser.Id, false, identityResult.Errors.Select(e => new Error(e.Code, e.Description)).ToList());
                }
            } catch (Exception e) {
                _logger.LogError($"{nameof(ChangePassword)} exception", e);
                return null;
            }
        }
        private async Task<FindUserResponse> getUser(AppUser appUser)
        {
            User user = appUser == null ? null : _mapper.Map(appUser, await GetSingleBySpec(new UserSpecification(appUser.Id)), opt => opt.ConfigureMap(MemberList.None));
            return user == null ? new FindUserResponse(null, false, new List<Error>() { new Error(null, "User not found!") }) : 
                new FindUserResponse(appUser.Id, true, null);
        }
    }
}