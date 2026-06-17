/* tslint:disable:no-unused-variable */
import { async, ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { DebugElement } from '@angular/core';

import { ControladoriaComponent } from './controladoria.component';

describe('ControladoriaComponent', () => {
  let component: ControladoriaComponent;
  let fixture: ComponentFixture<ControladoriaComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ ControladoriaComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ControladoriaComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
