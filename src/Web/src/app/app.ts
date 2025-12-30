import { Component, ViewChild, OnInit, OnDestroy, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule, MatDrawer } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { VersionService } from './services/version.service';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit, OnDestroy {
  title = 'Photos Index';

  @ViewChild('drawer') drawer!: MatDrawer;

  private breakpointObserver = inject(BreakpointObserver);
  private versionService = inject(VersionService);
  private breakpointSubscription?: any;

  isMobile = false;
  appVersion = this.versionService.getDisplayVersion();
  
  ngOnInit(): void {
    // Observe breakpoint changes to switch between mobile and desktop layouts
    this.breakpointSubscription = this.breakpointObserver
      .observe([Breakpoints.Handset, Breakpoints.Tablet])
      .subscribe(result => {
        this.isMobile = result.matches;
        
        if (this.drawer) {
          if (this.isMobile) {
            // On mobile: use 'over' mode and close by default
            this.drawer.mode = 'over';
            this.drawer.close();
          } else {
            // On desktop: use 'side' mode and open by default
            this.drawer.mode = 'side';
            this.drawer.open();
          }
        }
      });
  }
  
  ngOnDestroy(): void {
    this.breakpointSubscription?.unsubscribe();
  }
}
