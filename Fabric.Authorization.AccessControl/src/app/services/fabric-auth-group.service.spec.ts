import { HttpClient, HttpErrorResponse, HttpHeaders, HTTP_INTERCEPTORS } from '@angular/common/http';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { Pipe, PipeTransform } from '@angular/core';
import { TestBed, inject, async } from '@angular/core/testing';
import { Observable } from 'rxjs/Rx';

import { FabricAuthGroupService, AccessControlConfigService } from '../services';
import { Group, User, Role } from '../models';
import { ErrorObservable } from 'rxjs/observable/ErrorObservable';
import { FabricHttpErrorHandlerInterceptorService } from './interceptors/fabric-http-error-handler-interceptor.service';

fdescribe('FabricAuthGroupService', () => {

  const groupName = 'Dos Admin Group';
  const groupSource = 'Custom';

  const mockUsersResponse = [
    {
      name: 'First Last',
      subjectId: 'Sub123',
      identityProvider: 'Windows'
    }
  ];

  const mockRolesResponse = [
    {
      name: 'admin',
      grain: 'dos',
      securableItem: 'datamart',
      parentRole: 'admin_parent'
    },
    {
      name: 'superuser',
      grain: 'dos',
      securableItem: 'datamart',
      childRoles: [
        'dos_child1',
        'dos_child2'
      ]
    }
  ];

  const mockGroupResponse = {
    groupName: groupName,
    groupSource: groupSource,
    users: mockUsersResponse,
    roles: mockRolesResponse
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ HttpClientTestingModule ],
      providers: [
        FabricAuthGroupService,         
        { provide: HTTP_INTERCEPTORS, useClass: FabricHttpErrorHandlerInterceptorService, multi: true },
        AccessControlConfigService ]
    });
  });

  it('should be created', inject([FabricAuthGroupService], (service: FabricAuthGroupService) => {
    expect(service).toBeTruthy();
  }));

  it('getGroupUsers should deserialize all properties',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        service.getGroupUsers(groupName).subscribe(returnedUser => {
          assertMockGroupUsersResponse(returnedUser);
        });

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/users`));
        expect(req.request.method).toBe("GET");      
        req.flush(mockUsersResponse, {status: 200, statusText: 'OK'});        
        httpTestingController.verify();
      })
    )
  );

  it('getGroupUsers error should be caught',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        service.getGroupUsers(groupName).catch(error => {
          expect(Observable.of(error)).toBeTruthy();
          expect(error.statusCode).toBe(404);
          expect(error.message).toBe('Group not found');
          return Observable.of(error);
        })
        .subscribe();

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/users`));
        expect(req.request.method).toBe("GET");
        req.flush(null, {status: 404, statusText: 'Group not found'});        
        httpTestingController.verify();
      })
    )
  );

  it('addUsersToCustomGroup should deserialize all properties',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        let userRequest: User = new User("idp", "sub123");
        let userRequestArr: User[] = new Array<User>(userRequest);
        service.addUsersToCustomGroup(groupName, userRequestArr).subscribe(returnedGroup => {
          assertMockGroupResponse(returnedGroup);
        });

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/users`));
        expect(req.request.method).toBe("POST");

        let requestBody = JSON.stringify(req.request.body);
        expect(requestBody).toBe(JSON.stringify(userRequestArr));
        req.flush(mockGroupResponse, {status: 201, statusText: 'Created'});        
        httpTestingController.verify();
      })
    )
  );

  it('addUsersToCustomGroup error should be caught',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        service.addUsersToCustomGroup(groupName, new Array<User>()).catch(error => {
          expect(Observable.of(error)).toBeTruthy();
          expect(error.statusCode).toBe(404);
          expect(error.message).toBe('Group not found');
          return Observable.of(error);
        })
        .subscribe();

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/users`));
        expect(req.request.method).toBe("POST");
        req.flush(null, {status: 404, statusText: 'Group not found'});        
        httpTestingController.verify();
      })
    )
  );

  it('removeUserFromCustomGroup should deserialize all properties',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        let user = new User('idp', 'sub');

        service.removeUserFromCustomGroup(groupName, user).subscribe(returnedGroup => {
          assertMockGroupResponse(returnedGroup);
        });

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/users`));
        expect(req.request.method).toBe("DELETE");
        expect(req.request.body).toBeDefined();

        let requestBody = JSON.stringify(req.request.body);
        expect(requestBody).toBe(JSON.stringify(user));
        req.flush(mockGroupResponse, {status: 204, statusText: 'No Content'});        
        httpTestingController.verify();
      })
    )
  );

  it('removeUserFromCustomGroup error should be caught',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        service.removeUserFromCustomGroup(groupName, null).catch(error => {
          expect(Observable.of(error)).toBeTruthy();
          expect(error.statusCode).toBe(404);
          expect(error.message).toBe('Group not found');
          return Observable.of(error);
        })
        .subscribe();

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/users`));
        expect(req.request.method).toBe("DELETE");
        req.flush(null, {status: 404, statusText: 'Group not found'});        
        httpTestingController.verify();
      })
    )
  );

  it('getGroupRoles should deserialize all properties',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        service.getGroupRoles(groupName).subscribe(returnedGroup => {
          assertMockGroupRolesResponse(returnedGroup);
        });

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/roles`));
        expect(req.request.method).toBe("GET");      
        req.flush(mockRolesResponse, {status: 200, statusText: 'OK'});        
        httpTestingController.verify();
      })
    )
  );

  it('getGroupRoles error should be caught',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        service.getGroupRoles(groupName, null).catch(error => {
          expect(Observable.of(error)).toBeTruthy();
          expect(error.statusCode).toBe(404);
          expect(error.message).toBe('Group not found');
          return Observable.of(error);
        })
        .subscribe();

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/roles`));
        expect(req.request.method).toBe("GET");
        req.flush(null, {status: 404, statusText: 'Group not found'});        
        httpTestingController.verify();
      })
    )
  );

  it('addRolesToGroup should deserialize all properties',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {
        
        service.addRolesToGroup(groupName, null).subscribe(returnedGroup => {
          assertMockGroupResponse(returnedGroup);
        });

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/roles`));
        expect(req.request.method).toBe("POST");      
        req.flush(mockGroupResponse, {status: 201, statusText: 'Created'});
        httpTestingController.verify();
      })
    )
  );

  it('addRolesToGroup error should be caught',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        service.addRolesToGroup(groupName, null).catch(error => {
          expect(Observable.of(error)).toBeTruthy();
          expect(error.statusCode).toBe(404);
          expect(error.message).toBe('Group not found');
          return Observable.of(error);
        })
        .subscribe();

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/roles`));
        expect(req.request.method).toBe("POST");
        req.flush(null, {status: 404, statusText: 'Group not found'});        
        httpTestingController.verify();
      })
    )
  );

  it('removeRoleFromGroup should deserialize all properties',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        let role = new Role('admin', 'dos', 'datamart');
        role.parentRole = 'admin_parent';

        service.removeRoleFromGroup(groupName, role).subscribe(returnedGroup => {
          assertMockGroupResponse(returnedGroup);
        });

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/roles`));
        expect(req.request.method).toBe("DELETE");
        expect(req.request.body).toBeDefined();

        let requestBody = JSON.stringify(req.request.body);
        expect(requestBody).toBe(JSON.stringify(role));
        req.flush(mockGroupResponse, {status: 204, statusText: 'No Content'});        
        httpTestingController.verify();
      })
    )
  );

  it('removeRoleFromGroup error should be caught',
    async(  
      inject([HttpClient, HttpTestingController, FabricAuthGroupService], (
        httpClient: HttpClient,
        httpTestingController: HttpTestingController,
        service: FabricAuthGroupService) => {

        service.removeRoleFromGroup(groupName, null).catch(error => {
          expect(Observable.of(error)).toBeTruthy();
          expect(error.statusCode).toBe(404);
          expect(error.message).toBe('Group not found');
          return Observable.of(error);
        })
        .subscribe();

        const req = httpTestingController.expectOne(encodeURI(`${FabricAuthGroupService.baseGroupApiUrl}/${groupName}/roles`));
        expect(req.request.method).toBe("DELETE");
        req.flush(null, {status: 404, statusText: 'Group not found'});        
        httpTestingController.verify();
      })
    )
  );

  function assertMockGroupResponse(returnedGroup: Group) {
    expect(returnedGroup.groupName).toBe(groupName);
    expect(returnedGroup.groupSource).toBe(groupSource);
    assertMockGroupRolesResponse(returnedGroup.roles);
    assertMockGroupUsersResponse(returnedGroup.users);
  }

  function assertMockGroupRolesResponse(returnedRoles: Role[]) {
    expect(returnedRoles).toBeDefined();
    expect(returnedRoles.length).toBe(2);

    let adminRole = returnedRoles[0];
    expect(adminRole.name).toBe('admin');
    expect(adminRole.grain).toBe('dos');
    expect(adminRole.securableItem).toBe('datamart');
    expect(adminRole.parentRole).toBe('admin_parent');

    let superUserRole = returnedRoles[1];
    expect(superUserRole.name).toBe('superuser');
    expect(superUserRole.grain).toBe('dos');
    expect(superUserRole.securableItem).toBe('datamart');
    expect(superUserRole.childRoles).toBeDefined();
    expect(superUserRole.childRoles.length).toBe(2);
    expect(superUserRole.childRoles[0]).toBe('dos_child1');
    expect(superUserRole.childRoles[1]).toBe('dos_child2');
  }

  function assertMockGroupUsersResponse(returnedUsers: User[]) {
    expect(returnedUsers).toBeDefined();
    expect(returnedUsers.length).toBe(1);

    let returnedUser = returnedUsers[0];
    expect(returnedUser.subjectId).toEqual('Sub123');
    expect(returnedUser.identityProvider).toEqual('Windows');
    expect(returnedUser.name).toEqual('First Last');
  }
});
