/* tslint:disable:no-unused-variable */
import { async, ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { DebugElement } from '@angular/core';

import { MercadolivreComponent } from './mercadolivre.component';

describe('MercadolivreComponent', () => {
  let component: MercadolivreComponent;
  let fixture: ComponentFixture<MercadolivreComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ MercadolivreComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(MercadolivreComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
