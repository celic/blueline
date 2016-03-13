describe Enums::Position do
  subject { described_class }

  context '::parse' do
    %w(lw c rw d g).each_with_index do |position, index|
      it "should return the correct value for '#{position}'" do
        expect(subject.parse(position)).to eql index + 1
      end
    end
  end
end
