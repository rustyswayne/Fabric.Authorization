import {
  HTTP_INTERCEPTORS
} from '@angular/common/http';
import {
  HttpClientTestingModule
} from '@angular/common/http/testing';
import { FabricHttpErrorHandlerInterceptorService } from './interceptors/fabric-http-error-handler-interceptor.service';
import { TestBed, inject } from '@angular/core/testing';

import { FabricAuthEdwadminService } from './fabric-auth-edwadmin.service';
import { MockAccessControlConfigService } from './access-control-config.service.mock';

describe('FabricAuthEdwadminService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [FabricAuthEdwadminService,
        {
          provide: HTTP_INTERCEPTORS,
          useClass: FabricHttpErrorHandlerInterceptorService,
          multi: true
        },
        {
          provide: 'IAccessControlConfigService',
          useClass: MockAccessControlConfigService
        }]
    });
  });

  it('should be created', inject([FabricAuthEdwadminService], (service: FabricAuthEdwadminService) => {
    expect(service).toBeTruthy();
  }));
});