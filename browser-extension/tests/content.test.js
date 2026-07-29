"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const { ScrollAccumulator } = require("../scroll-accumulator.js");

test("coalesces multiple root scroll positions into one delta", () => {
  const accumulator = new ScrollAccumulator();
  accumulator.observe("root", 0, 100);
  accumulator.observe("root", 0, 125);

  assert.deepEqual(accumulator.flush(), [{
    target: "root",
    deltaXCss: 0,
    deltaYCss: 25
  }]);
});

test("keeps nested scroll targets separate", () => {
  const accumulator = new ScrollAccumulator();
  accumulator.observe("element-1", 10, 20);
  accumulator.observe("element-2", 30, 40);
  accumulator.observe("element-1", 13, 28);
  accumulator.observe("element-2", 27, 45);

  assert.deepEqual(accumulator.flush(), [
    {
      target: "element-1",
      deltaXCss: 3,
      deltaYCss: 8
    },
    {
      target: "element-2",
      deltaXCss: -3,
      deltaYCss: 5
    }
  ]);
});

test("does not emit unchanged positions", () => {
  const accumulator = new ScrollAccumulator();
  accumulator.observe("root", 4, 8);

  assert.equal(accumulator.observe("root", 4, 8), false);
  assert.deepEqual(accumulator.flush(), []);
});

test("ignores non-finite positions without poisoning the baseline", () => {
  const accumulator = new ScrollAccumulator();
  accumulator.observe("root", 0, 10);

  assert.equal(accumulator.observe("root", 0, Number.NaN), false);
  assert.equal(accumulator.observe("root", Number.POSITIVE_INFINITY, 20), false);
  accumulator.observe("root", 0, 15);

  assert.deepEqual(accumulator.flush(), [{
    target: "root",
    deltaXCss: 0,
    deltaYCss: 5
  }]);
});

test("invalidation advances generation and discards pending movement", () => {
  const accumulator = new ScrollAccumulator();
  accumulator.observe("root", 0, 10);
  accumulator.observe("root", 0, 30);

  assert.equal(accumulator.invalidate(), 1);
  assert.equal(accumulator.generation, 1);
  assert.deepEqual(accumulator.flush(), []);

  accumulator.observe("root", 0, 50);
  accumulator.observe("root", 0, 55);
  assert.deepEqual(accumulator.flush(), [{
    target: "root",
    deltaXCss: 0,
    deltaYCss: 5
  }]);
});

test("uses the latest nested-container geometry in a coalesced update", () => {
  const accumulator = new ScrollAccumulator();
  accumulator.observe("element-1", 0, 0);
  accumulator.observe("element-1", 0, 10, {
    scrollContainer: { left: 1, top: 2, width: 100, height: 80 }
  });
  accumulator.observe("element-1", 0, 15, {
    scrollContainer: { left: 3, top: 4, width: 100, height: 80 }
  });

  assert.deepEqual(accumulator.flush(), [{
    target: "element-1",
    deltaXCss: 0,
    deltaYCss: 15,
    scrollContainer: { left: 3, top: 4, width: 100, height: 80 }
  }]);
});
