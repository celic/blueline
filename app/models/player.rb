# == Schema Information
#
# Table name: players
#
#  id       :integer          not null, primary key
#  team_id  :integer
#  name     :string
#  position :integer
#  key      :string
#
# Indexes
#
#  index_players_on_key      (key) UNIQUE
#  index_players_on_team_id  (team_id)
#

class Player < ActiveRecord::Base
	extend EnumerateIt

	## Relationships
	belongs_to :team

	has_enumeration_for :position, with: Enums::Position

	## Scopes
	scope :forwards, lambda { where(position: Enums::Position.forwards) }
	scope :defensemen, lambda { where(position: Enums::Position::D) }
	scope :goalies, lambda { where(position: Enums::Position::G) }

	## Methods
	def forward?
		Enums::Position.forward? self.position
	end

	def defenseman?
		Enums::Position.defenseman? self.position
	end

	def goalie?
		Enums::Position.goalie? self.position
	end
end