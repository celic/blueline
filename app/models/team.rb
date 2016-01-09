# == Schema Information
#
# Table name: teams
#
#  id           :integer          not null, primary key
#  name         :string
#  abbreviation :string
#  city         :string
#  state        :string
#  full_name    :string
#  division_id  :integer
#  wins         :integer          default(0)
#  losses       :integer          default(0)
#  ot           :integer          default(0)
#  so           :integer          default(0)
#  points       :integer          default(0)
#
# Indexes
#
#  index_teams_on_abbreviation  (abbreviation)
#

class Team < ActiveRecord::Base

	## Callbacks
	before_create :generate_full_name

	## Relationships
	has_many :players

	## Scopes
	scope :division, lambda { |div| where(division_id: div) }

	## Class Methods
	def self.by_abbrev(abbv)
		self.find_by abbreviation: abbv.to_s.upcase
	end

	## Private Methods
	private
	def generate_full_name

		# set default full name if not set
		self.full_name ||= "#{self.city} #{self.name}"

		# continue with creation
		true
	end
end
