import { Injectable } from '@angular/core';
import { ToastrService } from 'ngx-toastr';

@Injectable({
  providedIn: 'root'
})
export class AlertService {

  constructor(private toastr: ToastrService) { }

  public showSyncWarning(errorMessage: string) {
    this.toastr.warning('Changes have been saved, however we were not able to sync the changes to EDW Console. If the user(s) need admin access to EDW Console contact your administrator to manually grant access. The error is: ' + errorMessage);
  }

  public showSaveError(errorMessage: string) {
    this.toastr.error('An error was encountered during save. The error was: ' + errorMessage);
  }
}
