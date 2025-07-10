// Test script to verify seat parsing logic
const testSeats = "H1;I35;J20";
console.log("Original seats string:", testSeats);

const parsedSeats = testSeats.split(';', StringSplitOptions.RemoveEmptyEntries);
console.log("Parsed seats:", parsedSeats);
console.log("Parsed seats count:", parsedSeats.length);

// Test with empty string
const emptySeats = "";
const parsedEmptySeats = emptySeats.split(';').filter(s => s.trim() !== '');
console.log("Empty seats parsed:", parsedEmptySeats);
console.log("Empty seats count:", parsedEmptySeats.length);

// Test with single seat
const singleSeat = "A1";
const parsedSingleSeat = singleSeat.split(';').filter(s => s.trim() !== '');
console.log("Single seat parsed:", parsedSingleSeat);
console.log("Single seat count:", parsedSingleSeat.length);
