# Ratatosk

A small shop: a Catalog of Products, the Stock held for each one, and Orders that claim that Stock. It is a reference implementation of DDD, event sourcing and CQRS.

## Catalog

**Product**:
Something the shop sells, with a Name, SKU, Description and Price.
_Avoid_: Item, article

**SKU**:
A Product's unique stock-keeping code (e.g. `TS-3FA7C1`). It identifies the same Product across the Catalog, Inventoring and Ordering contexts.
_Avoid_: Product code, article number

**Price**:
An amount in one supported currency.
_Avoid_: Cost, amount

**Product removal**:
Taking a Product out of the Catalog. Its Stock record remains, because Orders may still hold Reservations against it.
_Avoid_: Delete, discontinue

## Inventoring

**Inventory**:
The record of one Product's Stock. Each Product has exactly one, created when the Product is added.
_Avoid_: Warehouse, stock item

**Stock**:
The quantity of a Product the shop holds. It splits into **available** (on hand and unclaimed) and **reserved** (claimed but not yet shipped).
_Avoid_: Inventory level, supply

**Quantity**:
An amount together with its unit (pieces, kilograms, litres, …).
_Avoid_: Count, amount

**Restock**:
Adding Stock to an Inventory.
_Avoid_: Add stock, replenish

**Reservation**:
A claim on available Stock, either held by an Order or made by hand. A by-hand Reservation can never release Stock that an Order holds.
_Avoid_: Allocation, hold, booking

**Rejected reservation**:
A Reservation for an Order that the Inventory refused (unknown SKU or not enough available Stock). It is recorded on the Inventory.
_Avoid_: Failed reservation, stock-out

**Release**:
Returning reserved Stock to available. For an Order, it returns exactly what that Order holds.
_Avoid_: Unreserve, free, deallocate

## Ordering

**Customer**:
The party an Order is placed for.
_Avoid_: Client, buyer, account

**Order**:
A Customer's request for one or more Order lines, which is placed, then either confirmed or cancelled.
_Avoid_: Purchase, basket, cart

**Order line**:
One SKU on an Order, with a quantity and the unit Price at the time the Order was placed. An Order has at most one line per SKU.
_Avoid_: Item, row, position

**Order placement**:
Creating an Order from a Customer's lines at current Catalog prices. Reserving Stock for its lines follows from placement.
_Avoid_: Checkout, submit

**Confirmed**:
The status of an Order whose every Order line holds a Reservation.
_Avoid_: Accepted, complete

**Cancelled**:
The status of an Order that will not be fulfilled, together with the reason. Every Reservation it held is Released.
_Avoid_: Rejected, failed, void

**Compensation**:
Releasing the Stock a Cancelled Order holds.
_Avoid_: Rollback, undo

## Identity

**User**:
Someone who can sign in, identified by email address.
_Avoid_: Account, member, login

**Role**:
What a User may do: `User` or `Admin`.
_Avoid_: Permission, group
